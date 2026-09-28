// ============================================================================
// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Proprietary rights reserved except as expressly licensed herein.
//
// DO NOT PANIC PORTFOLIO VISUALIZER
// This file is governed by the SANYALnet Labs Non-Commercial License in the
// root LICENSE file. Commercial Use and use for AI/ML model training are
// prohibited unless separately authorized.
//
// Attribution is required: "Based on original work by Supratim Sanyal of
// SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
// patent, trademark, and governing-law provisions.
// ============================================================================
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

internal static class UdpSyslogTraceForwarder
{
    internal const string EnableEnvironmentVariable = "DNPPV_TRACE_FORWARD";
    internal const string TestOverrideEnvironmentVariable = "DNPPV_TRACE_FORWARD_TEST_OVERRIDE";
    internal const string HostEnvironmentVariable = "DNPPV_TRACE_FORWARD_HOST";
    internal const string PortEnvironmentVariable = "DNPPV_TRACE_FORWARD_PORT";
    internal const string ProductionHost = "sanyalnet-oracle-vps2.duckdns.org";
    internal const int ProductionPort = 65514;
    private const int MaximumQueueEntries = 2048;
    private const int MaximumDatagramBytes = 1200;

    private static readonly object QueueGate = new();
    private static readonly ConcurrentQueue<ForwardItem> Queue = new();
    private static readonly SemaphoreSlim QueueSignal = new(0);
    private static readonly CancellationTokenSource WorkerCancellation = new();
    private static int _workerStarted;

    internal static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable(EnableEnvironmentVariable), "Y", StringComparison.Ordinal) ||
           string.Equals(Environment.GetEnvironmentVariable(EnableEnvironmentVariable), "1", StringComparison.Ordinal);

    internal static void TryEnqueue(string level, string source, string line, string application)
    {
        if (!IsEnabled())
            return;

        (string host, int port) = ResolveEndpoint();
        lock (QueueGate)
        {
            if (Queue.Count >= MaximumQueueEntries)
                return;

            Queue.Enqueue(new ForwardItem(level, source, line, application, host, port));
        }
        EnsureWorker();
        QueueSignal.Release();
    }

    internal static void Shutdown()
        => WorkerCancellation.Cancel();

    internal static IReadOnlyList<byte[]> BuildDatagramsForTests(
        string level,
        string source,
        string line,
        string application,
        string host,
        int port)
        => BuildDatagrams(level, source, line, application, host, port);

    private static void EnsureWorker()
    {
        if (Interlocked.CompareExchange(ref _workerStarted, 1, 0) != 0)
            return;

        _ = Task.Run(ProcessQueueAsync);
    }

    private static async Task ProcessQueueAsync()
    {
        using UdpClient client = new(AddressFamily.InterNetwork);
        while (!WorkerCancellation.IsCancellationRequested)
        {
            try
            {
                await QueueSignal.WaitAsync(WorkerCancellation.Token).ConfigureAwait(false);
                if (!Queue.TryDequeue(out ForwardItem? item))
                    continue;

                using CancellationTokenSource dnsTimeout = new(TimeSpan.FromSeconds(2));
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(item.Host, dnsTimeout.Token).ConfigureAwait(false);
                IPAddress? address = addresses.FirstOrDefault(static candidate => candidate.AddressFamily == AddressFamily.InterNetwork);
                if (address is null)
                    continue;

                foreach (byte[] datagram in BuildDatagrams(item.Level, item.Source, item.Line, item.Application, item.Host, item.Port))
                    await client.SendAsync(datagram, new IPEndPoint(address, item.Port)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (WorkerCancellation.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Remote forwarding is best effort. Local circular traces remain authoritative.
            }
        }
    }

    private static (string Host, int Port) ResolveEndpoint()
    {
        string host = ProductionHost;
        int port = ProductionPort;
        if (string.Equals(Environment.GetEnvironmentVariable(TestOverrideEnvironmentVariable), "Y", StringComparison.Ordinal))
        {
            host = Environment.GetEnvironmentVariable(HostEnvironmentVariable) ?? host;
            if (!int.TryParse(Environment.GetEnvironmentVariable(PortEnvironmentVariable), out port) || port is < 1 or > 65535)
                port = ProductionPort;
        }

        return (host, port);
    }

    private static IReadOnlyList<byte[]> BuildDatagrams(
        string level,
        string source,
        string line,
        string application,
        string host,
        int port)
    {
        int priority = level switch
        {
            "ERROR" => 11,
            "WARN" => 12,
            _ => 14
        };
        string normalizedHost = SanitizeHeader(host, 255);
        string normalizedApplication = SanitizeHeader(application, 48);
        string normalizedSource = SanitizeHeader(source, 64);
        byte[] payload = Encoding.UTF8.GetBytes(line);
        int headerBytes = Encoding.UTF8.GetByteCount($"<{priority}>1 {DateTimeOffset.UtcNow:O} {normalizedHost} {normalizedApplication} - - [dnppv2 source=\"{normalizedSource}\" port=\"{port}\"] ");
        int chunkBytes = Math.Max(128, MaximumDatagramBytes - headerBytes - 32);
        int total = Math.Max(1, (payload.Length + chunkBytes - 1) / chunkBytes);
        List<byte[]> datagrams = [];
        for (int offset = 0, part = 1; offset < payload.Length || (payload.Length == 0 && part == 1); part++)
        {
            int count = Math.Min(chunkBytes, payload.Length - offset);
            if (payload.Length == 0)
                count = 0;

            string header = $"<{priority}>1 {DateTimeOffset.UtcNow:O} {normalizedHost} {normalizedApplication} - - [dnppv2 source=\"{normalizedSource}\" part=\"{part}/{total}\"] ";
            byte[] headerPayload = Encoding.UTF8.GetBytes(header);
            byte[] datagram = new byte[headerPayload.Length + count];
            Buffer.BlockCopy(headerPayload, 0, datagram, 0, headerPayload.Length);
            if (count > 0)
                Buffer.BlockCopy(payload, offset, datagram, headerPayload.Length, count);
            datagrams.Add(datagram);
            offset += count;
            if (payload.Length == 0)
                break;
        }

        return datagrams;
    }

    private static string SanitizeHeader(string value, int maximumLength)
        => string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Replace(' ', '_').Replace('\r', '_').Replace('\n', '_').Replace('"', '_')[..Math.Min(value.Length, maximumLength)];

    private sealed record ForwardItem(string Level, string Source, string Line, string Application, string Host, int Port);
}
