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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Shared.Services;
using YFinance.NET.Transport;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class YFinanceInfrastructureTests
{
    [Fact]
    public void ResolveRequestBaseUri_TreatsRootedApiPathAsYahooRelativeUri()
    {
        Uri baseUri = new("https://query1.finance.yahoo.com");

        Uri resolved = YahooFinanceHttpClient.ResolveRequestBaseUri("/v7/finance/quote", baseUri);

        Assert.Equal("https://query1.finance.yahoo.com/v7/finance/quote", resolved.AbsoluteUri);
    }

    [Fact]
    public void ResolveLaunchCommand_PrefersBundledExecutable()
    {
        using TemporaryDirectoryScope directory = new();
        string serverDirectory = Path.Combine(directory.Path, "YFinanceServer");
        Directory.CreateDirectory(serverDirectory);
        string executablePath = Path.Combine(serverDirectory, "YFinance.NET.Server.exe");
        File.WriteAllText(executablePath, string.Empty);

        YFinanceServerProcessManager manager = new(new YFinanceServerProcessManagerOptions
        {
            BaseDirectoryOverride = directory.Path
        });

        YFinanceServerLaunchCommand command = manager.ResolveLaunchCommand("token-123");

        Assert.Equal(executablePath, command.FileName);
        Assert.Equal(executablePath, command.ResolvedPath);
        Assert.Contains("--port 14871", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("--launch-token \"token-123\"", command.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveLaunchCommand_PrefersBundledExtensionlessExecutableOverDll()
    {
        using TemporaryDirectoryScope directory = new();
        string serverDirectory = Path.Combine(directory.Path, "YFinanceServer");
        Directory.CreateDirectory(serverDirectory);
        string executablePath = Path.Combine(serverDirectory, "YFinance.NET.Server");
        File.WriteAllText(executablePath, string.Empty);
        File.WriteAllText(executablePath + ".dll", string.Empty);

        YFinanceServerProcessManager manager = new(new YFinanceServerProcessManagerOptions
        {
            BaseDirectoryOverride = directory.Path
        });

        YFinanceServerLaunchCommand command = manager.ResolveLaunchCommand("token-linux");

        Assert.Equal(executablePath, command.FileName);
        Assert.Equal(executablePath, command.ResolvedPath);
        Assert.DoesNotContain("dotnet", command.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveLaunchCommand_FallsBackToDotNetForDll()
    {
        using TemporaryDirectoryScope directory = new();
        string serverDirectory = Path.Combine(directory.Path, "server");
        Directory.CreateDirectory(serverDirectory);
        string dllPath = Path.Combine(serverDirectory, "YFinance.NET.Server.dll");
        File.WriteAllText(dllPath, string.Empty);

        YFinanceServerProcessManager manager = new(new YFinanceServerProcessManagerOptions
        {
            BaseDirectoryOverride = directory.Path
        });

        YFinanceServerLaunchCommand command = manager.ResolveLaunchCommand("token-456");

        Assert.Equal("dotnet", command.FileName);
        Assert.Equal(dllPath, command.ResolvedPath);
        Assert.Contains($"\"{dllPath}\"", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("--launch-token \"token-456\"", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("--launch-token \"<redacted>\"", command.TraceArguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureOwnedServerAsync_DoesNotLaunchWhenPortIsAlreadyReachable()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        RecordingProcessLauncher launcher = new(new RecordingProcessHandle());
        YFinanceServerProcessManager manager = new(
            new YFinanceServerProcessManagerOptions
            {
                LoopbackPort = port
            },
            launcher);

        await manager.EnsureOwnedServerAsync("DNPPV.Tests");

        Assert.Equal(0, launcher.StartCallCount);
    }

    [Fact]
    public async Task EnsureOwnedServerAsync_StartsOwnedProcessAndWaitsForProbe()
    {
        using TemporaryDirectoryScope directory = new();
        string serverDirectory = Path.Combine(directory.Path, "YFinanceServer");
        Directory.CreateDirectory(serverDirectory);
        File.WriteAllText(Path.Combine(serverDirectory, "YFinance.NET.Server.exe"), string.Empty);

        RecordingProcessHandle handle = new();
        RecordingProcessLauncher launcher = new(handle);
        SequencedEndpointProbe probe = new(false, false, true);
        YFinanceServerProcessManager manager = new(
            new YFinanceServerProcessManagerOptions
            {
                BaseDirectoryOverride = directory.Path,
                StartupTimeout = TimeSpan.FromSeconds(5),
                StartupPollInterval = TimeSpan.FromMilliseconds(1),
                ConnectProbeTimeout = TimeSpan.FromMilliseconds(5)
            },
            launcher,
            probe);

        await manager.EnsureOwnedServerAsync("DNPPV.Tests");

        Assert.Equal(1, launcher.StartCallCount);
        Assert.NotNull(launcher.LastStartInfo);
        Assert.Contains("--port 14871", launcher.LastStartInfo!.Arguments, StringComparison.Ordinal);
        Assert.Equal(3, probe.CallCount);
    }

    [Fact]
    public async Task StopOwnedServerAsync_KillsOwnedProcessTree()
    {
        using TemporaryDirectoryScope directory = new();
        string serverDirectory = Path.Combine(directory.Path, "YFinanceServer");
        Directory.CreateDirectory(serverDirectory);
        File.WriteAllText(Path.Combine(serverDirectory, "YFinance.NET.Server.exe"), string.Empty);

        RecordingProcessHandle handle = new();
        RecordingProcessLauncher launcher = new(handle);
        SequencedEndpointProbe probe = new(false, true);
        YFinanceServerProcessManager manager = new(
            new YFinanceServerProcessManagerOptions
            {
                BaseDirectoryOverride = directory.Path,
                StartupTimeout = TimeSpan.FromSeconds(5),
                StartupPollInterval = TimeSpan.FromMilliseconds(1),
                ConnectProbeTimeout = TimeSpan.FromMilliseconds(5)
            },
            launcher,
            probe);

        await manager.EnsureOwnedServerAsync("DNPPV.Tests");
        await manager.StopOwnedServerAsync();

        Assert.True(handle.KillCalled);
        Assert.True(handle.WaitForExitCalled);
        Assert.True(handle.DisposeCalled);
    }

    [Fact]
    public async Task EnsureOwnedServerAsync_TerminatesOwnedProcessWhenStartupIsCanceled()
    {
        using TemporaryDirectoryScope directory = new();
        string serverDirectory = Path.Combine(directory.Path, "YFinanceServer");
        Directory.CreateDirectory(serverDirectory);
        File.WriteAllText(Path.Combine(serverDirectory, "YFinance.NET.Server.exe"), string.Empty);

        RecordingProcessHandle handle = new();
        RecordingProcessLauncher launcher = new(handle);
        SequencedEndpointProbe probe = new(false, false, false, false);
        YFinanceServerProcessManager manager = new(
            new YFinanceServerProcessManagerOptions
            {
                BaseDirectoryOverride = directory.Path,
                StartupTimeout = TimeSpan.FromSeconds(30),
                StartupPollInterval = TimeSpan.FromMilliseconds(25),
                ConnectProbeTimeout = TimeSpan.FromMilliseconds(5)
            },
            launcher,
            probe);

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(60));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.EnsureOwnedServerAsync("DNPPV.Tests / unsafe", cts.Token));

        Assert.True(handle.KillCalled);
        Assert.True(handle.DisposeCalled);
    }

    [Fact]
    public async Task ManagedYFinanceRuntimeClient_EnsuresOwnedServerBeforeDelegatingQuotes()
    {
        RecordingServerProcessManager serverManager = new();
        FakeYFinanceRuntimeClient innerClient = new()
        {
            QuotesAsync = (_, _) => Task.FromResult(new YFinanceQuotesResponse([]))
        };
        ManagedYFinanceRuntimeClient client = new(serverManager, innerClient, "DNPPV.Tests");

        await client.GetQuotesAsync(["AAPL"]);

        Assert.Equal(["DNPPV.Tests"], serverManager.ClientTypes);
        Assert.Equal(1, innerClient.GetQuotesCallCount);
    }

    private sealed class RecordingServerProcessManager : IYFinanceServerProcessManager
    {
        public List<string> ClientTypes { get; } = [];

        public Task EnsureOwnedServerAsync(string clientType, CancellationToken cancellationToken = default)
        {
            ClientTypes.Add(clientType);
            return Task.CompletedTask;
        }

        public Task StopOwnedServerAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingProcessLauncher(RecordingProcessHandle handle) : IYFinanceServerProcessLauncher
    {
        public int StartCallCount { get; private set; }
        public ProcessStartInfo? LastStartInfo { get; private set; }

        public IYFinanceServerProcessHandle Start(ProcessStartInfo startInfo)
        {
            StartCallCount++;
            LastStartInfo = startInfo;
            return handle;
        }
    }

    private sealed class RecordingProcessHandle : IYFinanceServerProcessHandle
    {
        public int Id => 4242;
        public bool HasExited { get; private set; }
        public int ExitCode { get; private set; }
        public bool KillCalled { get; private set; }
        public bool WaitForExitCalled { get; private set; }
        public bool DisposeCalled { get; private set; }

        public void Dispose() => DisposeCalled = true;

        public void Kill(bool entireProcessTree)
        {
            KillCalled = true;
            HasExited = true;
            ExitCode = 0;
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitForExitCalled = true;
            HasExited = true;
            return Task.CompletedTask;
        }
    }

    private sealed class SequencedEndpointProbe(params bool[] results) : IYFinanceLoopbackEndpointProbe
    {
        private readonly Queue<bool> _results = new(results);

        public int CallCount { get; private set; }

        public Task<bool> CanConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_results.Count == 0 ? true : _results.Dequeue());
        }
    }

    private sealed class TemporaryDirectoryScope : IDisposable
    {
        public TemporaryDirectoryScope()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dnppv2-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
