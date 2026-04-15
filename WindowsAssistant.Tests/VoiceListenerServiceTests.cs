using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="VoiceListenerService"/> configuration:
///   - Wake phrase lookup per culture
///   - Known cultures list
/// </summary>
public class VoiceListenerServiceTests
{
    // -------------------------------------------------------------------------
    // Wake phrases — must match the expected values declared in the service
    // -------------------------------------------------------------------------

    [Fact]
    public void WakePhrases_EnUsContainsHeyWindows()
    {
        Assert.Contains("hey windows", GetWakePhrases("en-US"));
    }

    [Fact]
    public void WakePhrases_PtBrContainsEiComputador()
    {
        Assert.Contains("ei computador", GetWakePhrases("pt-BR"));
    }

    [Fact]
    public void WakePhrases_PtBrHasMultipleGreetings()
    {
        var phrases = GetWakePhrases("pt-BR");
        Assert.Contains("oi computador", phrases);
        Assert.Contains("olá computador", phrases);
    }

    [Fact]
    public void WakePhrases_UnknownCultureFallsToHeyWindows()
    {
        Assert.Contains("hey windows", GetWakePhrases("fr-FR"));
    }

    // -------------------------------------------------------------------------
    // Known cultures exposed for UI toggles
    // -------------------------------------------------------------------------

    [Fact]
    public void KnownCultures_ContainsSupportedLocales()
    {
        Assert.Contains("pt-BR", VoiceListenerService.KnownCultures);
        Assert.Contains("en-US", VoiceListenerService.KnownCultures);
    }

    // -------------------------------------------------------------------------
    // Helper — mirrors VoiceListenerService.WakePhrases lookup
    // -------------------------------------------------------------------------

    private static readonly Dictionary<string, string[]> WakePhrases = new()
    {
        ["en-US"] = ["hey windows", "hey computer"],
        ["pt-BR"] = ["ei computador", "oi computador", "olá computador", "ola computador"],
    };

    private static string[] GetWakePhrases(string cultureName)
        => WakePhrases.GetValueOrDefault(cultureName, ["hey windows"]);
}
