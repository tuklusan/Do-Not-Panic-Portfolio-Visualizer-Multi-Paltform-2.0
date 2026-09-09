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
using System.Net;
using System.Net.Sockets;
using System.Text;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

namespace DoNotPanicPortfolioVisualizer.Tests;

[Collection("TraceForwarding")]
public sealed class UdpSyslogTraceForwarderTests
{
    [Fact]
    public async Task EnabledForwardingDeliversCompletePayloadToLocalUdpReceiver()
    {
        using UdpClient receiver = new(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
        string? previousEnabled = Environment.GetEnvironmentVariable(UdpSyslogTraceForwarder.EnableEnvironmentVariable);
        string? previousOverride = Environment.GetEnvironmentVariable(UdpSyslogTraceForwarder.TestOverrideEnvironmentVariable);
        string? previousHost = Environment.GetEnvironmentVariable(UdpSyslogTraceForwarder.HostEnvironmentVariable);
        string? previousPort = Environment.GetEnvironmentVariable(UdpSyslogTraceForwarder.PortEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.EnableEnvironmentVariable, "Y");
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.TestOverrideEnvironmentVariable, "Y");
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.HostEnvironmentVariable, "127.0.0.1");
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.PortEnvironmentVariable, port.ToString());

            const string line = "event=CompletePayload | content=all-of-the-source-event";
            UdpSyslogTraceForwarder.TryEnqueue("INFO", "UdpSyslogTraceForwarderTests", line, "tests");
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            UdpReceiveResult result = await receiver.ReceiveAsync(timeout.Token);
            string payload = Encoding.UTF8.GetString(result.Buffer);

            Assert.Contains("<14>1 ", payload, StringComparison.Ordinal);
            Assert.Contains("source=\"UdpSyslogTraceForwarderTests\"", payload, StringComparison.Ordinal);
            Assert.Contains(line, payload, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.EnableEnvironmentVariable, previousEnabled);
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.TestOverrideEnvironmentVariable, previousOverride);
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.HostEnvironmentVariable, previousHost);
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.PortEnvironmentVariable, previousPort);
        }
    }

    [Fact]
    public void OversizedPayloadIsSplitIntoBoundedSyslogDatagrams()
    {
        string line = new('x', 8_000);
        IReadOnlyList<byte[]> datagrams = UdpSyslogTraceForwarder.BuildDatagramsForTests(
            "WARN", "OversizeTest", line, "tests", "127.0.0.1", 65514);

        Assert.True(datagrams.Count > 1);
        Assert.All(datagrams, datagram => Assert.InRange(datagram.Length, 1, 1200));
        Assert.Contains(datagrams, datagram => Encoding.UTF8.GetString(datagram).Contains("part=\"1/", StringComparison.Ordinal));
        Assert.Contains(datagrams, datagram => Encoding.UTF8.GetString(datagram).Contains(line[..128], StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("y")]
    public void ForwardingRequiresExactOptInValue(string? value)
    {
        string? previous = Environment.GetEnvironmentVariable(UdpSyslogTraceForwarder.EnableEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.EnableEnvironmentVariable, value);
            Assert.False(UdpSyslogTraceForwarder.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable(UdpSyslogTraceForwarder.EnableEnvironmentVariable, previous);
        }
    }
}

[CollectionDefinition("TraceForwarding", DisableParallelization = true)]
public sealed class TraceForwardingCollection
{
}
