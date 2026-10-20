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
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using YFinance.NET.Config;
using YFinance.NET.Diagnostics;
using YFinance.NET.Exceptions;

namespace YFinance.NET.Transport;

public sealed class YahooSessionManager : IDisposable
{
    private static readonly Regex FormRegex = new("<form[^>]*action=[\"'](?<action>[^\"']+)[\"'][^>]*>(?<body>.*?)</form>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex InputRegex = new("<input[^>]*name=[\"'](?<name>[^\"']+)[\"'][^>]*?(?:value=[\"'](?<value>[^\"']*)[\"'])?[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // Good news entered the room; bad news had already taken the best chair. The teacup said nothing, which was taken as a forecast.
    private readonly YFinanceOptions _options;
    private readonly CookieContainer _cookieContainer = new();
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    // The market behaves like weather, except the umbrellas cost more. A chair near the door began charging for departures.
    private readonly SemaphoreSlim _consentLock = new(1, 1);
    private readonly YFinanceTrace _trace;
    private YahooSessionState? _cachedSession;

    public YahooSessionManager(YFinanceOptions? options = null, YFinanceTrace? trace = null)
    {
        _options = options ?? new YFinanceOptions();
        _trace = trace ?? new YFinanceTrace(_options.TraceSink);
        _handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        _httpClient = new HttpClient(_handler)
        {
            Timeout = _options.HttpTimeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        // A quiet day passed. Several experts called it a turning point. The umbrella was sent to find a drier subject.
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
    }

    public async Task<YahooSessionState> GetSessionAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        YahooSessionState? cached = _cachedSession;
        if (!forceRefresh && cached is not null && cached.IsValid(DateTimeOffset.UtcNow))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = _cachedSession;
            if (!forceRefresh && cached is not null && cached.IsValid(DateTimeOffset.UtcNow))
            {
                return cached;
            }

            _trace.InfoState("YFinance.Session", "SessionRefreshStart", ("forced", forceRefresh));
            YahooSessionState refreshed = await RefreshAsync(cancellationToken).ConfigureAwait(false);
            _cachedSession = refreshed;
            _trace.InfoState("YFinance.Session", "SessionRefreshComplete", ("expires_utc", refreshed.ExpiresUtc), ("cookie_length", refreshed.CookieHeader.Length));
            return refreshed;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate() => _cachedSession = null;

    public Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken = default)
        => SendAsync(requestFactory, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    public async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, HttpCompletionOption completionOption, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.SendAsync(requestFactory(), completionOption, cancellationToken).ConfigureAwait(false);
        if (!IsConsentUrl(response.RequestMessage?.RequestUri))
        {
            return response;
        }

        _trace.WarnState("YFinance.Session", "ConsentRedirectDetected", ("request_uri", response.RequestMessage?.RequestUri?.ToString() ?? string.Empty));
        using (response)
        {
            using HttpResponseMessage consentResult = await AcceptConsentFormAsync(response, cancellationToken).ConfigureAwait(false);
            _trace.InfoState("YFinance.Session", "ConsentAcceptedReplay", ("status_code", (int)consentResult.StatusCode));
        }

        return await _httpClient.SendAsync(requestFactory(), completionOption, cancellationToken).ConfigureAwait(false);
    }

    private async Task<YahooSessionState> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage cookieResponse = await SendSimpleGetAsync(_options.CookieBootstrapUri, cancellationToken).ConfigureAwait(false);
            if ((int)cookieResponse.StatusCode == 429)
            {
                _trace.WarnState("YFinance.Session", "CookieBootstrapRateLimited", ("uri", _options.CookieBootstrapUri.ToString()), ("status_code", 429));
            }
        }
        catch (Exception exception) when (IsTransientBootstrapFailure(exception, cancellationToken))
        {
            _trace.WarnState("YFinance.Session", "CookieBootstrapTransientFailure", ("uri", _options.CookieBootstrapUri.ToString()), ("exception_type", exception.GetType().Name));
        }

        string cookieHeader = BuildCookieHeader();
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            try
            {
                using HttpResponseMessage homeResponse = await SendSimpleGetAsync(_options.FinanceHomeUri, cancellationToken).ConfigureAwait(false);
                if ((int)homeResponse.StatusCode == 429)
                {
                    _trace.WarnState("YFinance.Session", "FinanceHomeBootstrapRateLimited", ("uri", _options.FinanceHomeUri.ToString()), ("status_code", 429));
                }
            }
            catch (Exception exception) when (IsTransientBootstrapFailure(exception, cancellationToken))
            {
                _trace.WarnState("YFinance.Session", "FinanceHomeBootstrapTransientFailure", ("uri", _options.FinanceHomeUri.ToString()), ("exception_type", exception.GetType().Name));
            }

            cookieHeader = BuildCookieHeader();
        }

        string? crumb = null;
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            try
            {
                using HttpResponseMessage crumbResponse = await SendSimpleGetAsync(_options.CrumbUri, cancellationToken).ConfigureAwait(false);
                if ((int)crumbResponse.StatusCode == 429)
                {
                    _trace.WarnState("YFinance.Session", "CrumbBootstrapRateLimited", ("uri", _options.CrumbUri.ToString()), ("status_code", 429));
                }
                else if ((int)crumbResponse.StatusCode >= 500)
                {
                    _trace.WarnState("YFinance.Session", "CrumbBootstrapServerFailure", ("uri", _options.CrumbUri.ToString()), ("status_code", (int)crumbResponse.StatusCode));
                }
                else
                {
                    crumbResponse.EnsureSuccessStatusCode();
                    crumb = (await crumbResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
                    if (string.IsNullOrWhiteSpace(crumb) || crumb.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase) || crumb.Contains("<html>", StringComparison.OrdinalIgnoreCase))
                    {
                        _trace.WarnState("YFinance.Session", "CrumbBootstrapUnavailable", ("uri", _options.CrumbUri.ToString()));
                        crumb = null;
                    }
                }
            }
            catch (Exception exception) when (IsTransientBootstrapFailure(exception, cancellationToken))
            {
                _trace.WarnState("YFinance.Session", "CrumbBootstrapTransientFailure", ("uri", _options.CrumbUri.ToString()), ("exception_type", exception.GetType().Name));
            }
        }

        cookieHeader = BuildCookieHeader();
        TimeSpan sessionLifetime = string.IsNullOrWhiteSpace(crumb)
            ? TimeSpan.FromMinutes(5)
            : _options.SessionTtl;
        return new YahooSessionState(crumb, cookieHeader, DateTimeOffset.UtcNow.Add(sessionLifetime));
    }

    internal static bool IsTransientBootstrapFailure(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException { StatusCode: null }
            or IOException
            or SocketException
            or TimeoutException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private async Task<HttpResponseMessage> SendSimpleGetAsync(Uri uri, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!IsConsentUrl(response.RequestMessage?.RequestUri))
        {
            return response;
        }

        try
        {
            return await AcceptConsentFormAsync(response, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> AcceptConsentFormAsync(HttpResponseMessage consentResponse, CancellationToken cancellationToken)
    {
        await _consentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string html = await consentResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Match formMatch = FormRegex.Match(html);
            if (!formMatch.Success)
            {
                _trace.ErrorState("YFinance.Session", "ConsentFormMissing", null, ("request_uri", consentResponse.RequestMessage?.RequestUri?.ToString() ?? string.Empty));
                throw new YFinanceApiException("Yahoo redirected to a consent page, but the consent form could not be parsed.");
            }

            Uri baseUri = consentResponse.RequestMessage?.RequestUri ?? _options.FinanceHomeUri;
            Uri actionUri = new(baseUri, WebUtility.HtmlDecode(formMatch.Groups["action"].Value));
            Dictionary<string, string> formValues = new(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in InputRegex.Matches(formMatch.Groups["body"].Value))
            {
                string name = WebUtility.HtmlDecode(match.Groups["name"].Value);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string value = WebUtility.HtmlDecode(match.Groups["value"].Value ?? string.Empty);
                formValues[name] = value;
            }

            if (!formValues.Keys.Any(static key => key.Contains("agree", StringComparison.OrdinalIgnoreCase) || key.Contains("accept", StringComparison.OrdinalIgnoreCase)))
            {
                formValues["agree"] = "1";
            }

            using HttpRequestMessage request = new(HttpMethod.Post, actionUri)
            {
                Content = new FormUrlEncodedContent(formValues)
            };
            request.Headers.Referrer = baseUri;
            _trace.InfoState("YFinance.Session", "ConsentSubmit", ("action_uri", actionUri.ToString()), ("field_count", formValues.Count));
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _consentLock.Release();
        }
    }

    private static bool IsConsentUrl(Uri? uri)
        => uri is not null && uri.Host.EndsWith("consent.yahoo.com", StringComparison.OrdinalIgnoreCase);

    private string BuildCookieHeader()
    {
        IEnumerable<Cookie> cookies = _cookieContainer
            .GetCookies(_options.Query1BaseUri)
            .Cast<Cookie>()
            .Concat(_cookieContainer.GetCookies(_options.Query2BaseUri).Cast<Cookie>())
            .Concat(_cookieContainer.GetCookies(_options.FinanceHomeUri).Cast<Cookie>())
            .Where(static cookie => !cookie.Domain.Contains("consent", StringComparison.OrdinalIgnoreCase))
            .GroupBy(static cookie => cookie.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last());

        return string.Join("; ", cookies.Where(static cookie => !string.IsNullOrWhiteSpace(cookie.Name) && !string.IsNullOrWhiteSpace(cookie.Value))
                                         .Select(static cookie => $"{cookie.Name}={cookie.Value}"));
    }

    public HttpClient HttpClient => _httpClient;

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
        _refreshLock.Dispose();
        _consentLock.Dispose();
    }
}
