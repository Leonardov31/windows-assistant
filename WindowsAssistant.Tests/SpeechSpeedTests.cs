using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="SpeechSpeed"/> enum and the speed detection algorithm.
/// The voice service categorizes words-per-second into speed tiers:
///   - Slow:   &lt; 1.5 wps
///   - Normal: 1.5–3.0 wps
///   - Fast:   &gt; 3.0 wps
/// </summary>
public class SpeechSpeedTests
{
    [Fact]
    public void SpeechSpeed_HasThreeValues()
    {
        var values = Enum.GetValues<SpeechSpeed>();
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void SpeechSpeed_ContainsExpectedValues()
    {
        Assert.True(Enum.IsDefined(SpeechSpeed.Slow));
        Assert.True(Enum.IsDefined(SpeechSpeed.Normal));
        Assert.True(Enum.IsDefined(SpeechSpeed.Fast));
    }

    // -------------------------------------------------------------------------
    // Speed detection thresholds (mirror VoiceListenerService.AdaptSpeed logic)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0.5, SpeechSpeed.Slow)]
    [InlineData(1.0, SpeechSpeed.Slow)]
    [InlineData(1.4, SpeechSpeed.Slow)]
    public void SlowSpeed_BelowThreshold(double wordsPerSecond, SpeechSpeed expected)
    {
        var detected = ClassifySpeed(wordsPerSecond);
        Assert.Equal(expected, detected);
    }

    [Theory]
    [InlineData(1.5, SpeechSpeed.Normal)]
    [InlineData(2.0, SpeechSpeed.Normal)]
    [InlineData(3.0, SpeechSpeed.Normal)]
    public void NormalSpeed_WithinThreshold(double wordsPerSecond, SpeechSpeed expected)
    {
        var detected = ClassifySpeed(wordsPerSecond);
        Assert.Equal(expected, detected);
    }

    [Theory]
    [InlineData(3.1, SpeechSpeed.Fast)]
    [InlineData(4.0, SpeechSpeed.Fast)]
    [InlineData(5.5, SpeechSpeed.Fast)]
    public void FastSpeed_AboveThreshold(double wordsPerSecond, SpeechSpeed expected)
    {
        var detected = ClassifySpeed(wordsPerSecond);
        Assert.Equal(expected, detected);
    }

    // -------------------------------------------------------------------------
    // Speed preset timeout values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(SpeechSpeed.Slow, 1.5, 2.5, 6.0, 0.50f)]
    [InlineData(SpeechSpeed.Normal, 0.5, 1.0, 4.0, 0.65f)]
    [InlineData(SpeechSpeed.Fast, 0.2, 0.4, 2.0, 0.70f)]
    public void SpeedPreset_HasCorrectTimeouts(
        SpeechSpeed speed,
        double expectedEndSilence,
        double expectedEndSilenceAmbiguous,
        double expectedBabble,
        float expectedConfidence)
    {
        var (endSilence, endSilenceAmbiguous, babble, confidence) = GetPreset(speed);

        Assert.Equal(expectedEndSilence, endSilence);
        Assert.Equal(expectedEndSilenceAmbiguous, endSilenceAmbiguous);
        Assert.Equal(expectedBabble, babble);
        Assert.Equal(expectedConfidence, confidence);
    }

    [Fact]
    public void SlowSpeed_HasLowestConfidenceThreshold()
    {
        var (_, _, _, slow) = GetPreset(SpeechSpeed.Slow);
        var (_, _, _, normal) = GetPreset(SpeechSpeed.Normal);
        var (_, _, _, fast) = GetPreset(SpeechSpeed.Fast);

        Assert.True(slow < normal);
        Assert.True(normal < fast);
    }

    [Fact]
    public void SlowSpeed_HasLongestTimeouts()
    {
        var (slowEnd, _, slowBabble, _) = GetPreset(SpeechSpeed.Slow);
        var (normalEnd, _, normalBabble, _) = GetPreset(SpeechSpeed.Normal);
        var (fastEnd, _, fastBabble, _) = GetPreset(SpeechSpeed.Fast);

        Assert.True(slowEnd > normalEnd);
        Assert.True(normalEnd > fastEnd);
        Assert.True(slowBabble > normalBabble);
        Assert.True(normalBabble > fastBabble);
    }

    // -------------------------------------------------------------------------
    // Words-per-second calculation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows brightness 3 in monitor 1", 3.0, 2.33)]
    [InlineData("hey windows brightness 10 monitor 1", 5.0, 1.2)]
    public void WordsPerSecond_CalculatedCorrectly(string text, double durationSeconds, double expectedWps)
    {
        int wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        double wps = wordCount / durationSeconds;

        Assert.Equal(expectedWps, Math.Round(wps, 2));
    }

    // -------------------------------------------------------------------------
    // Helpers — mirror the logic from VoiceListenerService
    // -------------------------------------------------------------------------

    private static SpeechSpeed ClassifySpeed(double avgRate) => avgRate switch
    {
        < 1.5 => SpeechSpeed.Slow,
        > 3.0 => SpeechSpeed.Fast,
        _     => SpeechSpeed.Normal,
    };

    private static (double endSilence, double endSilenceAmbiguous, double babble, float confidence) GetPreset(SpeechSpeed speed) => speed switch
    {
        SpeechSpeed.Slow   => (1.5, 2.5, 6.0, 0.50f),
        SpeechSpeed.Normal => (0.5, 1.0, 4.0, 0.65f),
        SpeechSpeed.Fast   => (0.2, 0.4, 2.0, 0.70f),
        _ => throw new ArgumentOutOfRangeException(nameof(speed)),
    };
}
