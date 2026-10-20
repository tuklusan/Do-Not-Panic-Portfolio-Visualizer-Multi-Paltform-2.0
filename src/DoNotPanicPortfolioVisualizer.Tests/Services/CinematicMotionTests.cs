using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Render.Services;
using Xunit;

namespace DoNotPanicPortfolioVisualizer.Tests.Services;

public sealed class CinematicMotionTests
{
    [Fact]
    public void WorldMarketTapeAccountsForAFrameGapLongerThanOneHundredMilliseconds()
    {
        GlobalMarketsMotionController motion = new();
        motion.Configure(viewportWidth: 900d, scrollingMarketCount: 8);

        motion.Step(TimeSpan.FromMilliseconds(200));

        Assert.Equal(-6d, motion.Offset, precision: 6);
    }

    [Fact]
    public void TickerMotionAccountsForAFrameGapLongerThanOneHundredMilliseconds()
    {
        TickerMotionController motion = new();
        motion.Configure(cycleDistance: 1000d, configuredSpeed: 0.25d, ScrollDirection.Left, sideCopies: 0);

        motion.Step(TimeSpan.FromMilliseconds(200));

        Assert.Equal(-3.6d, motion.Offset, precision: 6);
    }

    [Fact]
    public void NewsTypewriterAccountsForAFrameGapLongerThanOneHundredMilliseconds()
    {
        NewsPlaybackController playback = new();
        playback.SetHeadlines(["abcdefghij"]);
        playback.Step(TimeSpan.Zero);

        playback.Step(TimeSpan.FromMilliseconds(200));

        Assert.StartsWith("ABCDE", playback.DisplayText, StringComparison.Ordinal);
    }
}
