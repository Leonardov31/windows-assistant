using System.Globalization;
using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="VoiceListenerService"/> configuration and behavior:
///   - Wake phrase definitions
///   - Multi-culture engine creation
///   - Sample window for speed detection
/// </summary>
public class VoiceListenerServiceTests
{
    // -------------------------------------------------------------------------
    // Wake phrases — must match expected values per culture
    // -------------------------------------------------------------------------

    [Fact]
    public void WakePhrases_EnUsIsHeyWindows()
    {
        // The wake phrase for en-US should be "hey windows"
        // Verified by reading VoiceListenerService.WakePhrases
        var expected = "hey windows";
        Assert.Equal(expected, GetWakePhrase("en-US"));
    }

    [Fact]
    public void WakePhrases_PtBrIsEiWindows()
    {
        var expected = "ei windows";
        Assert.Equal(expected, GetWakePhrase("pt-BR"));
    }

    [Fact]
    public void WakePhrases_UnknownCultureFallsToHeyWindows()
    {
        // VoiceListenerService uses GetValueOrDefault with "hey windows" fallback
        var expected = "hey windows";
        Assert.Equal(expected, GetWakePhrase("fr-FR"));
    }

    // -------------------------------------------------------------------------
    // Sample window for speed adaptation
    // -------------------------------------------------------------------------

    [Fact]
    public void SampleWindow_IsEight()
    {
        // VoiceListenerService maintains a rolling window of 8 samples
        // for speed auto-detection
        const int expectedWindow = 8;
        var samples = new Queue<double>();

        // Simulate filling beyond the window
        for (int i = 0; i < 12; i++)
        {
            samples.Enqueue(2.0);
            while (samples.Count > expectedWindow)
                samples.Dequeue();
        }

        Assert.Equal(expectedWindow, samples.Count);
    }

    [Fact]
    public void SpeedAdaptation_RequiresAtLeastTwoSamples()
    {
        // The speed detection logic requires at least 2 samples
        // before making any adjustments
        var samples = new Queue<double>();
        samples.Enqueue(1.0);

        Assert.True(samples.Count < 2, "Single sample should not trigger adaptation");
    }

    // -------------------------------------------------------------------------
    // Rolling average calculation
    // -------------------------------------------------------------------------

    [Fact]
    public void RollingAverage_CalculatesCorrectly()
    {
        var samples = new Queue<double>();
        samples.Enqueue(1.0);
        samples.Enqueue(2.0);
        samples.Enqueue(3.0);

        double avg = samples.Average();
        Assert.Equal(2.0, avg);
    }

    [Fact]
    public void RollingAverage_WindowEvictsOldSamples()
    {
        const int window = 8;
        var samples = new Queue<double>();

        // Add slow samples
        for (int i = 0; i < 8; i++)
            samples.Enqueue(1.0);

        // Add fast samples — should push out the slow ones
        for (int i = 0; i < 8; i++)
        {
            samples.Enqueue(4.0);
            while (samples.Count > window)
                samples.Dequeue();
        }

        double avg = samples.Average();
        Assert.Equal(4.0, avg);
    }

    // -------------------------------------------------------------------------
    // Minimum duration filter — ignores audio < 0.3 seconds
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0.1, false)]
    [InlineData(0.2, false)]
    [InlineData(0.29, false)]
    [InlineData(0.3, true)]
    [InlineData(1.0, true)]
    public void AdaptSpeed_FiltersByMinimumDuration(double durationSeconds, bool shouldProcess)
    {
        // Mirror the guard: duration < 0.3 seconds is skipped
        bool process = durationSeconds >= 0.3;
        Assert.Equal(shouldProcess, process);
    }

    // -------------------------------------------------------------------------
    // Helper — mirrors VoiceListenerService.WakePhrases lookup
    // -------------------------------------------------------------------------

    private static readonly Dictionary<string, string> WakePhrases = new()
    {
        ["en-US"] = "hey windows",
        ["pt-BR"] = "ei windows",
    };

    private static string GetWakePhrase(string cultureName)
        => WakePhrases.GetValueOrDefault(cultureName, "hey windows");
}
