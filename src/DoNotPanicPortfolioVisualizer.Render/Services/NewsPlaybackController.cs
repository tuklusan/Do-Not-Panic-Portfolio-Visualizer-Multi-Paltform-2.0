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
using System.Text;

namespace DoNotPanicPortfolioVisualizer.Render.Services;

public enum NewsPlaybackPhase
{
    Idle,
    Typing,
    PauseBeforeScroll,
    Scrolling,
    PauseAfterScroll,
    PauseBetweenHeadlines,
    AdvanceHeadline
}

public sealed class NewsPlaybackController
{
    public const double VisibleLineHeight = 19d;
    public const double VerticalScrollPixelsPerSecond = 42d;
    public const double TypewriterCharactersPerSecond = 50d;
    public const double RevealPauseSeconds = 0.35d;
    public const double PostScrollPauseSeconds = 0.25d;
    public const double BetweenHeadlinePauseSeconds = 1.6d;
    private const string TeleprinterCursor = " █";
    private const double ApproximateCharacterWidth = 9.6d;
    private const double MaximumFrameSeconds = 0.1d;

    private IReadOnlyList<string> _headlines = [];
    private IReadOnlyList<string> _wrappedLines = [];
    private int _headlineIndex;
    private int _segmentIndex;
    private int _visibleCharacterCount;
    private int _charactersPerLine = 80;
    private double _typingRemainder;
    private double _phaseSecondsRemaining;
    private string _topLine = string.Empty;
    private string _bottomLine = string.Empty;

    public NewsPlaybackPhase Phase { get; private set; } = NewsPlaybackPhase.Idle;
    public string DisplayText { get; private set; } = string.Empty;
    public double VerticalOffset { get; private set; }
    public double Speed { get; set; } = 1d;
    public int HeadlineIndex => _headlineIndex;
    public int SegmentIndex => _segmentIndex;

    public void ConfigureViewport(double width)
    {
        int nextCharactersPerLine = Math.Max(12, (int)Math.Floor(Math.Max(1d, width) / ApproximateCharacterWidth));
        if (nextCharactersPerLine == _charactersPerLine)
            return;

        _charactersPerLine = nextCharactersPerLine;
        Reset(preserveHeadlineIndex: true);
    }

    public void SetHeadlines(IEnumerable<string> headlines)
    {
        ArgumentNullException.ThrowIfNull(headlines);
        IReadOnlyList<string> normalized = headlines
            .Select(NormalizeHeadline)
            .Where(static headline => !string.IsNullOrWhiteSpace(headline))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (_headlines.SequenceEqual(normalized, StringComparer.Ordinal))
            return;

        _headlines = normalized;
        Reset(preserveHeadlineIndex: false);
    }

    public void Step(TimeSpan elapsed)
    {
        if (_headlines.Count == 0)
        {
            DisplayText = string.Empty;
            Phase = NewsPlaybackPhase.Idle;
            return;
        }

        double seconds = Math.Clamp(elapsed.TotalSeconds, 0d, MaximumFrameSeconds);
        switch (Phase)
        {
            case NewsPlaybackPhase.Idle:
                PrepareHeadline();
                break;
            case NewsPlaybackPhase.Typing:
                StepTyping(seconds);
                break;
            case NewsPlaybackPhase.PauseBeforeScroll:
                StepPause(seconds, NewsPlaybackPhase.Scrolling, includeCursor: true);
                break;
            case NewsPlaybackPhase.Scrolling:
                StepScrolling(seconds);
                break;
            case NewsPlaybackPhase.PauseAfterScroll:
                StepPause(seconds, GetPostScrollNextPhase(), includeCursor: false);
                break;
            case NewsPlaybackPhase.PauseBetweenHeadlines:
                StepPause(seconds, NewsPlaybackPhase.AdvanceHeadline, includeCursor: false);
                break;
            case NewsPlaybackPhase.AdvanceHeadline:
                AdvanceHeadline();
                break;
        }
    }

    private void PrepareHeadline()
    {
        _wrappedLines = Wrap(_headlines[_headlineIndex % _headlines.Count], _charactersPerLine);
        _segmentIndex = 0;
        PrepareSegment();
    }

    private void PrepareSegment()
    {
        _topLine = _wrappedLines.Count > _segmentIndex ? _wrappedLines[_segmentIndex] : string.Empty;
        _bottomLine = _wrappedLines.Count > _segmentIndex + 1 ? _wrappedLines[_segmentIndex + 1] : string.Empty;
        _visibleCharacterCount = 0;
        _typingRemainder = 0d;
        _phaseSecondsRemaining = 0d;
        VerticalOffset = 0d;
        Phase = NewsPlaybackPhase.Typing;
        DisplayText = TeleprinterCursor;
    }

    private void StepTyping(double seconds)
    {
        string fullText = GetSegmentText();
        int targetLength = _segmentIndex == 0 ? fullText.Length : _bottomLine.Length;
        _typingRemainder += TypewriterCharactersPerSecond * seconds;
        int characters = (int)Math.Floor(_typingRemainder);
        if (characters == 0)
            return;

        _typingRemainder -= characters;
        _visibleCharacterCount = Math.Min(targetLength, _visibleCharacterCount + characters);
        DisplayText = BuildVisibleText(_visibleCharacterCount < targetLength);
        if (_visibleCharacterCount < targetLength)
            return;

        Phase = NewsPlaybackPhase.PauseBeforeScroll;
        _phaseSecondsRemaining = RevealPauseSeconds;
    }

    private void StepScrolling(double seconds)
    {
        DisplayText = GetSegmentText();
        double targetOffset = string.IsNullOrWhiteSpace(_bottomLine) ? 0d : -VisibleLineHeight;
        VerticalOffset = Math.Max(
            targetOffset,
            VerticalOffset - VerticalScrollPixelsPerSecond * Math.Max(0.7d, Speed) * seconds);
        if (VerticalOffset > targetOffset + 0.1d)
            return;

        NewsPlaybackPhase nextPhase = GetPostScrollNextPhase();
        Phase = nextPhase == NewsPlaybackPhase.PauseBetweenHeadlines
            ? NewsPlaybackPhase.PauseBetweenHeadlines
            : NewsPlaybackPhase.PauseAfterScroll;
        _phaseSecondsRemaining = Phase == NewsPlaybackPhase.PauseBetweenHeadlines
            ? BetweenHeadlinePauseSeconds
            : PostScrollPauseSeconds;
    }

    private void StepPause(double seconds, NewsPlaybackPhase nextPhase, bool includeCursor)
    {
        DisplayText = includeCursor ? BuildVisibleText(includeCursor: true) : GetSegmentText();
        _phaseSecondsRemaining -= seconds;
        if (_phaseSecondsRemaining <= 0d)
            Phase = nextPhase;
    }

    private void AdvanceHeadline()
    {
        if (_segmentIndex + 1 < GetSegmentCount())
        {
            _segmentIndex++;
            PrepareSegment();
            return;
        }

        _headlineIndex = (_headlineIndex + 1) % _headlines.Count;
        Phase = NewsPlaybackPhase.Idle;
    }

    private NewsPlaybackPhase GetPostScrollNextPhase()
        => _segmentIndex + 1 < GetSegmentCount()
            ? NewsPlaybackPhase.AdvanceHeadline
            : NewsPlaybackPhase.PauseBetweenHeadlines;

    private int GetSegmentCount() => _wrappedLines.Count <= 1 ? _wrappedLines.Count : _wrappedLines.Count - 1;

    private string GetSegmentText()
    {
        if (string.IsNullOrWhiteSpace(_topLine))
            return _bottomLine;
        if (string.IsNullOrWhiteSpace(_bottomLine))
            return _topLine;
        return _topLine + Environment.NewLine + _bottomLine;
    }

    private string BuildVisibleText(bool includeCursor)
    {
        string visible;
        if (_segmentIndex == 0)
        {
            string fullText = GetSegmentText();
            visible = fullText[..Math.Min(_visibleCharacterCount, fullText.Length)];
        }
        else
        {
            string typedBottom = _bottomLine[..Math.Min(_visibleCharacterCount, _bottomLine.Length)];
            visible = string.IsNullOrWhiteSpace(_topLine)
                ? typedBottom
                : _topLine + Environment.NewLine + typedBottom;
        }

        return includeCursor ? visible + TeleprinterCursor : visible;
    }

    private void Reset(bool preserveHeadlineIndex)
    {
        if (!preserveHeadlineIndex)
            _headlineIndex = 0;
        else if (_headlines.Count > 0)
            _headlineIndex %= _headlines.Count;

        _wrappedLines = [];
        _segmentIndex = 0;
        _visibleCharacterCount = 0;
        _typingRemainder = 0d;
        _phaseSecondsRemaining = 0d;
        _topLine = string.Empty;
        _bottomLine = string.Empty;
        DisplayText = string.Empty;
        VerticalOffset = 0d;
        Phase = NewsPlaybackPhase.Idle;
    }

    private static string NormalizeHeadline(string headline)
    {
        StringBuilder cleaned = new();
        bool priorWhitespace = false;
        foreach (char character in (headline ?? string.Empty).Trim())
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (!priorWhitespace)
                    cleaned.Append(' ');
                priorWhitespace = true;
                continue;
            }

            cleaned.Append(char.ToUpperInvariant(character));
            priorWhitespace = false;
        }

        return cleaned.ToString().Trim();
    }

    private static IReadOnlyList<string> Wrap(string text, int maximumCharacters)
    {
        List<string> lines = [];
        string current = string.Empty;
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (candidate.Length <= maximumCharacters || current.Length == 0)
            {
                current = candidate;
                continue;
            }

            lines.Add(current);
            current = word;
        }

        if (current.Length > 0)
            lines.Add(current);
        return lines;
    }
}
