// ============================================================================
// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Proprietary rights reserved except as expressly licensed herein.
//
// DO NOT PANIC PORTFOLIO VISUALIZER
// This file is governed by the SANYALnet Labs Non-Commercial License in the
// root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
// for AI/ML model training are prohibited unless separately authorized.
//
// Attribution is required: "Based on original work by Supratim Sanyal of
// SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
// patent, trademark, and governing-law provisions.
// ============================================================================
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using DoNotPanicPortfolioVisualizer.Core;

namespace DoNotPanicPortfolioVisualizer.Shared.Services;

public interface IYFinanceServerProcessManager
{
    Task EnsureOwnedServerAsync(string clientType, CancellationToken cancellationToken = default);
    Task StopOwnedServerAsync(CancellationToken cancellationToken = default);
}

public interface IYFinanceLoopbackEndpointProbe
{
    Task<bool> CanConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default);
}

public interface IYFinanceServerProcessLauncher
{
    IYFinanceServerProcessHandle Start(ProcessStartInfo startInfo);
}

public interface IYFinanceServerProcessHandle : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    void Kill(bool entireProcessTree);
    Task WaitForExitAsync(CancellationToken cancellationToken);
}

public sealed record YFinanceServerLaunchCommand(
    string FileName,
    string Arguments,
    string TraceArguments,
    string ResolvedPath);

public sealed class YFinanceServerProcessManagerOptions
{
    public string? BaseDirectoryOverride { get; init; }
    public string LoopbackHost { get; init; } = YFinanceLoopbackContract.LoopbackHost;
    public int LoopbackPort { get; init; } = YFinanceLoopbackContract.DefaultPort;
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan StartupPollInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan ConnectProbeTimeout { get; init; } = TimeSpan.FromMilliseconds(500);
    public string DotNetCommand { get; init; } = "dotnet";
    public Action<string>? DiagnosticSink { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(LoopbackHost))
            throw new InvalidOperationException("A non-empty loopback host is required.");

        if (LoopbackPort is <= 0 or > 65535)
            throw new InvalidOperationException("The YFinance loopback port must be between 1 and 65535.");

        if (StartupTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("The YFinance server startup timeout must be positive.");

        if (StartupPollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("The YFinance server startup poll interval must be positive.");

        if (ConnectProbeTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("The YFinance loopback connect probe timeout must be positive.");

        if (string.IsNullOrWhiteSpace(DotNetCommand))
            throw new InvalidOperationException("A non-empty dotnet launcher command is required.");
    }
}

public sealed class YFinanceServerProcessManager : IYFinanceServerProcessManager, IDisposable
{
    private readonly YFinanceServerProcessManagerOptions _options;
    private readonly IYFinanceServerProcessLauncher _processLauncher;
    private readonly IYFinanceLoopbackEndpointProbe _endpointProbe;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly IEqualityComparer<string> _pathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private readonly ConcurrentDictionary<string, string> _resolvedServerPathByBaseDirectory;
    private IYFinanceServerProcessHandle? _ownedProcess;

    public YFinanceServerProcessManager(
        YFinanceServerProcessManagerOptions? options = null,
        IYFinanceServerProcessLauncher? processLauncher = null,
        IYFinanceLoopbackEndpointProbe? endpointProbe = null)
    {
        _options = options ?? new YFinanceServerProcessManagerOptions();
        _options.Validate();
        _processLauncher = processLauncher ?? new DefaultYFinanceServerProcessLauncher();
        _endpointProbe = endpointProbe ?? new DefaultYFinanceLoopbackEndpointProbe();
        _resolvedServerPathByBaseDirectory = new ConcurrentDictionary<string, string>(_pathComparer);
    }

    public async Task EnsureOwnedServerAsync(string clientType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientType);

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ownedProcess is not null)
            {
                if (!_ownedProcess.HasExited)
                    return;

                _ownedProcess.Dispose();
                _ownedProcess = null;
            }

            if (await CanConnectAsync(cancellationToken).ConfigureAwait(false))
                return;

            string token = $"{SanitizeClientTypeForLaunchToken(clientType)}-{Environment.ProcessId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            YFinanceServerLaunchCommand command = ResolveLaunchCommand(token);
            ProcessStartInfo startInfo = new()
            {
                FileName = command.FileName,
                Arguments = command.Arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                _ownedProcess = _processLauncher.Start(startInfo);
            }
            catch (Exception ex)
            {
                _options.DiagnosticSink?.Invoke(
                    $"YFinance server launch failed. file={command.FileName}; arguments={command.TraceArguments}; exception_type={ex.GetType().FullName}");
                _options.DiagnosticSink?.Invoke($"YFinance server launch exception message: {ex.Message}");
                throw new InvalidOperationException(
                    $"Failed to start {YFinanceLoopbackContract.ServerExecutableFileStem}.",
                    ex);
            }

            Stopwatch startupStopwatch = Stopwatch.StartNew();
            while (startupStopwatch.Elapsed < _options.StartupTimeout)
            {
                if (await CanConnectAsync(cancellationToken).ConfigureAwait(false))
                    return;

                if (_ownedProcess.HasExited)
                {
                    int exitCode = _ownedProcess.ExitCode;
                    _options.DiagnosticSink?.Invoke(
                        $"YFinance server exited during startup. file={command.FileName}; arguments={command.TraceArguments}; exit_code={exitCode}");
                    _ownedProcess.Dispose();
                    _ownedProcess = null;
                    throw new InvalidOperationException(
                        $"{YFinanceLoopbackContract.ServerExecutableFileStem} exited early with code {exitCode}.");
                }

                TimeSpan remaining = _options.StartupTimeout - startupStopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    break;

                await Task.Delay(
                    remaining < _options.StartupPollInterval ? remaining : _options.StartupPollInterval,
                    cancellationToken).ConfigureAwait(false);
            }

            TryTerminateOwnedProcess();
            throw new TimeoutException(
                $"Timed out waiting for {YFinanceLoopbackContract.ServerExecutableFileStem} to accept loopback connections on port {_options.LoopbackPort}.");
        }
        catch (OperationCanceledException) when (_ownedProcess is not null)
        {
            TryTerminateOwnedProcess();
            throw;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task StopOwnedServerAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ownedProcess is null)
                return;

            try
            {
                if (!_ownedProcess.HasExited)
                {
                    _ownedProcess.Kill(entireProcessTree: true);
                    await _ownedProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _ownedProcess.Dispose();
                _ownedProcess = null;
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    public YFinanceServerLaunchCommand ResolveLaunchCommand(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string baseDirectory = Path.GetFullPath(_options.BaseDirectoryOverride ?? AppContext.BaseDirectory);
        if (TryGetCachedLaunchPath(baseDirectory) is string cachedPath)
            return BuildLaunchCommandForPath(cachedPath, token);

        string[] candidateRoots =
        [
            Path.Combine(baseDirectory, YFinanceLoopbackContract.ServerBundleFolderName),
            Path.Combine(baseDirectory, "server"),
            baseDirectory
        ];

        foreach (string root in candidateRoots)
        {
            string exeCandidate = Path.Combine(root, YFinanceLoopbackContract.ServerExecutableFileStem + ".exe");
            if (File.Exists(exeCandidate))
            {
                CacheResolvedLaunchPath(baseDirectory, exeCandidate);
                return BuildLaunchCommandForPath(exeCandidate, token);
            }

            string nativeCandidate = Path.Combine(root, YFinanceLoopbackContract.ServerExecutableFileStem);
            if (File.Exists(nativeCandidate))
            {
                CacheResolvedLaunchPath(baseDirectory, nativeCandidate);
                return BuildLaunchCommandForPath(nativeCandidate, token);
            }

            string dllCandidate = Path.Combine(root, YFinanceLoopbackContract.ServerExecutableFileStem + ".dll");
            if (File.Exists(dllCandidate))
            {
                CacheResolvedLaunchPath(baseDirectory, dllCandidate);
                return BuildLaunchCommandForPath(dllCandidate, token);
            }
        }

        throw new FileNotFoundException(
            $"Could not locate {YFinanceLoopbackContract.ServerExecutableFileStem}. Expected it under the application {YFinanceLoopbackContract.ServerBundleFolderName} folder, a sibling server folder, or the application base directory.");
    }

    private async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        => await _endpointProbe.CanConnectAsync(
            _options.LoopbackHost,
            _options.LoopbackPort,
            _options.ConnectProbeTimeout,
            cancellationToken).ConfigureAwait(false);

    private void CacheResolvedLaunchPath(string baseDirectory, string path)
        => _resolvedServerPathByBaseDirectory[baseDirectory] = path;

    private string? TryGetCachedLaunchPath(string baseDirectory)
    {
        if (!_resolvedServerPathByBaseDirectory.TryGetValue(baseDirectory, out string? cachedPath))
            return null;

        if (File.Exists(cachedPath))
            return cachedPath;

        _resolvedServerPathByBaseDirectory.TryRemove(baseDirectory, out _);
        return null;
    }

    private YFinanceServerLaunchCommand BuildLaunchCommandForPath(string path, string token)
    {
        bool isDll = string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
        string arguments = BuildArguments(token);
        string traceArguments = BuildArguments("<redacted>");
        return isDll
            ? new YFinanceServerLaunchCommand(_options.DotNetCommand, $"\"{path}\" {arguments}", $"\"{path}\" {traceArguments}", path)
            : new YFinanceServerLaunchCommand(path, arguments, traceArguments, path);
    }

    public void Dispose()
    {
        TryTerminateOwnedProcess();
        _sync.Dispose();
    }

    private string BuildArguments(string token)
        => $"--port {_options.LoopbackPort} --owned --owner-pid {Environment.ProcessId} --max-clients 1024 --launch-token {QuoteArgument(token)}";

    private static string SanitizeClientTypeForLaunchToken(string clientType)
    {
        string sanitized = new(
            clientType
                .Trim()
                .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
                .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "client" : sanitized;
    }

    private static string QuoteArgument(string value)
        => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private void TryTerminateOwnedProcess()
    {
        if (_ownedProcess is null)
            return;

        try
        {
            if (!_ownedProcess.HasExited)
                _ownedProcess.Kill(entireProcessTree: true);
        }
        catch
        {
        }
        finally
        {
            _ownedProcess.Dispose();
            _ownedProcess = null;
        }
    }

    private sealed class DefaultYFinanceLoopbackEndpointProbe : IYFinanceLoopbackEndpointProbe
    {
        public async Task<bool> CanConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            try
            {
                using TcpClient client = new();
                using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);
                await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }
    }

    private sealed class DefaultYFinanceServerProcessLauncher : IYFinanceServerProcessLauncher
    {
        public IYFinanceServerProcessHandle Start(ProcessStartInfo startInfo)
        {
            Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Process.Start returned null.");
            return new DefaultYFinanceServerProcessHandle(process);
        }
    }

    private sealed class DefaultYFinanceServerProcessHandle(Process process) : IYFinanceServerProcessHandle
    {
        public int Id => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;

        public void Dispose() => process.Dispose();

        public void Kill(bool entireProcessTree) => process.Kill(entireProcessTree);

        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
    }
}
