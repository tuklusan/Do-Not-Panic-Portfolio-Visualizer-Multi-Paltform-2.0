using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class MarketSessionDisplayPropagationTests
{
    [Theory]
    [InlineData(MarketSession.PreMarket)]
    [InlineData(MarketSession.Regular)]
    [InlineData(MarketSession.AfterHours)]
    [InlineData(MarketSession.Closed)]
    public void SessionStateIsCarriedAcrossTickerMacroGraphAndWorldCards(MarketSession session)
    {
        QuoteSnapshot quote = new()
        {
            Symbol = "TEST",
            Last = 100m,
            PreviousClose = 99m,
            ChangePercent = 1.01m,
            MarketSession = session,
            FetchTimestampUtc = DateTimeOffset.UtcNow
        };

        TickerQuoteViewModel ticker = new(new TickerItem { Symbol = "TEST" });
        ticker.Apply(quote);
        MacroQuoteViewModel macro = new("TEST", "TEST", 200m);
        macro.Apply(quote);
        FloatingGraphViewModel graph = new() { Symbol = "TEST" };
        graph.MarketSession = session;
        GlobalMarketViewModel world = new()
        {
            Key = "Test",
            City = "Test",
            ExchangeName = "Test Exchange",
            Symbol = "TEST",
            TimeZoneId = "UTC",
            Latitude = 0,
            Longitude = 0
        };
        world.ApplyQuote(quote);

        Assert.Equal(session, ticker.MarketSession);
        Assert.Equal(session, macro.MarketSession);
        Assert.Equal(session, graph.MarketSession);
        Assert.Equal(session, world.MarketSession);
    }

    [Fact]
    public void WorldCardCalendarStatusOverridesUnknownQuoteSessionAndDisplaysCountdown()
    {
        GlobalMarketViewModel world = new()
        {
            Key = "NewYork",
            City = "New York",
            ExchangeName = "NASDAQ",
            Symbol = "^IXIC",
            TimeZoneId = "America/New_York",
            Latitude = 40.7,
            Longitude = -74.0
        };
        world.ApplyQuote(new QuoteSnapshot { Symbol = "^IXIC", MarketSession = MarketSession.Unknown });

        world.ApplyCalendarStatus(MarketSession.PreMarket, "PRE 00:30");

        Assert.Equal(MarketSession.PreMarket, world.MarketSession);
        Assert.Equal("PRE 00:30", world.SessionText);
    }
}
