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
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.Data.Runtime;

public sealed class ManagedYFinanceRuntimeClient : IYFinanceRuntimeClient
{
    private readonly IYFinanceServerProcessManager _serverProcessManager;
    private readonly IYFinanceRuntimeClient _innerClient;
    private readonly string _clientType;

    public ManagedYFinanceRuntimeClient(
        IYFinanceServerProcessManager serverProcessManager,
        IYFinanceRuntimeClient innerClient,
        string clientType)
    {
        _serverProcessManager = serverProcessManager ?? throw new ArgumentNullException(nameof(serverProcessManager));
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _clientType = string.IsNullOrWhiteSpace(clientType)
            ? throw new ArgumentException("A non-empty client type is required.", nameof(clientType))
            : clientType.Trim();
    }

    public async Task<YFinanceQuotesResponse> GetQuotesAsync(
        IReadOnlyList<string> requestSymbols,
        CancellationToken cancellationToken = default)
    {
        await _serverProcessManager.EnsureOwnedServerAsync(_clientType, cancellationToken).ConfigureAwait(false);
        return await _innerClient.GetQuotesAsync(requestSymbols, cancellationToken).ConfigureAwait(false);
    }

    public async Task<YFinanceHistoryResponse> GetHistoryAsync(
        string requestSymbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string interval,
        CancellationToken cancellationToken = default)
    {
        await _serverProcessManager.EnsureOwnedServerAsync(_clientType, cancellationToken).ConfigureAwait(false);
        return await _innerClient.GetHistoryAsync(
            requestSymbol,
            startUtc,
            endUtc,
            interval,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await _serverProcessManager.EnsureOwnedServerAsync(_clientType, cancellationToken).ConfigureAwait(false);
        return await _innerClient.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
    }
}
