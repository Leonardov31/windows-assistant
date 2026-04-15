using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for the pure wake-phrase extraction helpers used by the two-phase
/// state machine in <see cref="VoiceListenerService"/>:
///
///   - <see cref="VoiceListenerService.TryExtractCommand"/> — finds the wake
///     phrase anywhere in the text, returns the tail as the command candidate
///   - <see cref="VoiceListenerService.IsWithinWordLimit"/> — enforces the
///     6-word cap on the command portion
///   - <see cref="VoiceListenerService.CanStillMatchWakePhrase"/> — decides
///     whether to abort an in-flight dictation hypothesis early
/// </summary>
public class WakePhraseDetectionTests
{
    // -------------------------------------------------------------------------
    // TryExtractCommand — wake phrase anywhere, tail is the command
    // -------------------------------------------------------------------------

    [Fact]
    public void TryExtractCommand_returns_empty_tail_when_wake_is_only_word()
    {
        bool ok = VoiceListenerService.TryExtractCommand("computador", "computador", out var tail);
        Assert.True(ok);
        Assert.Equal(string.Empty, tail);
    }

    [Fact]
    public void TryExtractCommand_returns_command_after_wake_at_start()
    {
        bool ok = VoiceListenerService.TryExtractCommand("computador brilho cinco", "computador", out var tail);
        Assert.True(ok);
        Assert.Equal("brilho cinco", tail);
    }

    [Fact]
    public void TryExtractCommand_finds_wake_in_the_middle_and_discards_prefix()
    {
        // User says "uh, computador brilho cinco" — the "uh," is ignored.
        bool ok = VoiceListenerService.TryExtractCommand("uh computador brilho cinco", "computador", out var tail);
        Assert.True(ok);
        Assert.Equal("brilho cinco", tail);
    }

    [Fact]
    public void TryExtractCommand_strips_leading_punctuation_from_tail()
    {
        bool ok = VoiceListenerService.TryExtractCommand("computador, brilho cinco", "computador", out var tail);
        Assert.True(ok);
        Assert.Equal("brilho cinco", tail);
    }

    [Fact]
    public void TryExtractCommand_is_case_insensitive()
    {
        bool ok = VoiceListenerService.TryExtractCommand("Computador BRILHO cinco", "computador", out var tail);
        Assert.True(ok);
        Assert.Equal("BRILHO cinco", tail);
    }

    [Fact]
    public void TryExtractCommand_returns_false_when_wake_absent()
    {
        bool ok = VoiceListenerService.TryExtractCommand("hello world", "computador", out var tail);
        Assert.False(ok);
        Assert.Equal(string.Empty, tail);
    }

    [Theory]
    [InlineData("",                "computador")]
    [InlineData("computador brilho", "")]
    [InlineData("computador brilho", "   ")]
    public void TryExtractCommand_returns_false_on_empty_inputs(string text, string wake)
    {
        bool ok = VoiceListenerService.TryExtractCommand(text, wake, out var tail);
        Assert.False(ok);
        Assert.Equal(string.Empty, tail);
    }

    // -------------------------------------------------------------------------
    // IsWithinWordLimit — 6-word cap on command portion
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("brilho")]
    [InlineData("brilho cinco no primeiro")]
    [InlineData("one two three four five six")]               // exactly 6
    [InlineData("brightness five on monitor number one")]     // exactly 6
    public void IsWithinWordLimit_accepts_up_to_six_words(string command)
        => Assert.True(VoiceListenerService.IsWithinWordLimit(command));

    [Theory]
    [InlineData("one two three four five six seven")]         // 7
    [InlineData("ajusta o brilho do primeiro monitor para cinquenta")]  // 8
    [InlineData("set the brightness on my first monitor to fifty")]    // 9
    public void IsWithinWordLimit_rejects_more_than_six_words(string command)
        => Assert.False(VoiceListenerService.IsWithinWordLimit(command));

    [Fact]
    public void IsWithinWordLimit_collapses_repeated_spaces()
    {
        // "a  b   c" is 3 words once empty entries are removed.
        Assert.True(VoiceListenerService.IsWithinWordLimit("a  b   c"));
    }

    // -------------------------------------------------------------------------
    // CanStillMatchWakePhrase — used by the HypothesisGenerated early-abort
    // latch while in AwaitingWake. Rule: while the hypothesis is shorter than
    // the wake phrase, always keep listening; once it has reached wake-phrase
    // length it must start with the wake phrase.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("",           "computador")]
    [InlineData("c",          "computador")]
    [InlineData("como",       "computador")]   // wrongly aborted under naive prefix-only check
    [InlineData("computa",    "computador")]
    [InlineData("xyz",        "computador")]
    [InlineData("hello",      "hey windows")]  // 5 < 11
    public void CanStillMatchWakePhrase_true_when_hypothesis_shorter_than_wake(string hyp, string wake)
        => Assert.True(VoiceListenerService.CanStillMatchWakePhrase(hyp, wake));

    [Theory]
    [InlineData("computador",              "computador")]
    [InlineData("computador brilho",       "computador")]
    [InlineData("hey windows brightness",  "hey windows")]
    public void CanStillMatchWakePhrase_true_when_hypothesis_starts_with_wake(string hyp, string wake)
        => Assert.True(VoiceListenerService.CanStillMatchWakePhrase(hyp, wake));

    [Theory]
    [InlineData("hello world",     "computador")]
    [InlineData("brightness five", "computador")]
    [InlineData("hay windows",     "hey windows")]
    public void CanStillMatchWakePhrase_false_when_long_enough_and_no_match(string hyp, string wake)
        => Assert.False(VoiceListenerService.CanStillMatchWakePhrase(hyp, wake));
}
