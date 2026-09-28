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
using DoNotPanicPortfolioVisualizer.Shared;
using YFinance.NET.Client;
using YFinance.NET.Protocol.Dtos;

namespace DoNotPanicPortfolioVisualizer.Data.Runtime;

public sealed class YFinanceProtocolRuntimeClient : IYFinanceRuntimeClient, IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _helloGate = new(1, 1);
    private readonly YFinanceServerClient _client;
    private bool _helloCompleted;

    public YFinanceProtocolRuntimeClient(
        string host = Core.YFinanceLoopbackContract.LoopbackHost,
        int port = Core.YFinanceLoopbackContract.DefaultPort,
        TimeSpan? connectTimeout = null)
    {
        _client = new YFinanceServerClient(new YFinanceServerConnectionOptions(
            host,
            port,
            connectTimeout ?? TimeSpan.FromSeconds(10),
            NullYFinanceServerClientTraceSink.Instance));
    }

    public async Task<YFinanceQuotesResponse> GetQuotesAsync(
        IReadOnlyList<string> requestSymbols,
        CancellationToken cancellationToken = default)
    {
        await EnsureHelloAsync(cancellationToken).ConfigureAwait(false);
        QuotesResponseDto response = await _client.GetQuotesAsync(requestSymbols, cancellationToken).ConfigureAwait(false);
        return new YFinanceQuotesResponse(
            response.Quotes.Select(MapQuote).ToList(),
            response.MissingSymbols);
    }

    public async Task<YFinanceHistoryResponse> GetHistoryAsync(
        string requestSymbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string interval,
        CancellationToken cancellationToken = default)
    {
        await EnsureHelloAsync(cancellationToken).ConfigureAwait(false);
        HistoryResponseDto response = await _client.GetHistoryAsync(
            requestSymbol,
            startUtc,
            endUtc,
            interval,
            cancellationToken).ConfigureAwait(false);
        return new YFinanceHistoryResponse(
            response.Bars.Select(static bar => new YFinanceHistoryBar(bar.TimestampUtc, bar.Close)).ToList(),
            response.Metadata is null
                ? null
                : new YFinanceHistoryMetadata(response.Metadata.ExchangeTimezoneName));
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureHelloAsync(cancellationToken).ConfigureAwait(false);
            HealthResponseDto health = await _client.HealthAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(health.Status, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task EnsureHelloAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _helloCompleted))
            return;

        await _helloGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_helloCompleted)
                return;

            await _client.ConnectAsync(
                new HelloRequestDto(
                    "DNPPV-2.0",
                    PortfolioVersion.Version,
                    Environment.MachineName,
                    OwnedMode: true,
                    OwnerProcessId: Environment.ProcessId),
                cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _helloCompleted, true);
        }
        finally
        {
            _helloGate.Release();
        }
    }

    private static YFinanceQuoteResponse MapQuote(QuoteDto quote)
        => new(
            quote.Symbol,
            quote.RegularMarketPrice,
            quote.RegularMarketPreviousClose,
            quote.RegularMarketChange,
            quote.RegularMarketChangePercent,
            quote.Currency,
            quote.ExchangeTimezoneName,
            quote.MarketState,
            new YFinanceCacheMetadata(quote.Cache.Stale));

    public void Dispose()
    {
        _helloGate.Dispose();
        _client.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _helloGate.Dispose();
        await _client.DisposeAsync().ConfigureAwait(false);
    }
}
