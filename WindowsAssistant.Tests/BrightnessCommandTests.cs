using System.Globalization;
using System.Text.RegularExpressions;
using WindowsAssistant.Commands;
using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="BrightnessCommandHandler"/>:
///   - Short form: "{monitor} {value}"
///   - Long form 1: "{brightness} {value} {prep} {monitor}"
///   - Long form 2: "{monitor} {brightness} {value}"
///   - All targets: "both/all/ambos/todos {value}"
///   - Ordinal targets (first/primeiro...)
///   - Grammar building, handler metadata
/// </summary>
public class BrightnessCommandTests
{
    private static readonly Regex ShortPattern = BrightnessCommandHandler.ShortPattern;
    private static readonly Regex AllPattern = BrightnessCommandHandler.AllPattern;
    private static readonly Regex Long1Pattern = BrightnessCommandHandler.Long1Pattern;
    private static readonly Regex Long2Pattern = BrightnessCommandHandler.Long2Pattern;
    private static readonly Regex VerbLedPattern = BrightnessCommandHandler.VerbLedPattern;
    private static readonly Regex NumberFirstPattern = BrightnessCommandHandler.NumberFirstPattern;
    private static readonly Regex NoTargetPattern = BrightnessCommandHandler.NoTargetPattern;

    // =========================================================================
    // SHORT FORM: "{monitor} {value}"
    // =========================================================================

    [Theory]
    [InlineData("hey windows monitor 1 50", "monitor 1", 50)]
    [InlineData("hey windows monitor 2 20", "monitor 2", 20)]
    [InlineData("hey windows first 5", "first", 5)]
    [InlineData("hey windows second 100", "second", 100)]
    [InlineData("ei windows primeiro 8", "primeiro", 8)]
    [InlineData("ei windows segundo 50", "segundo", 50)]
    [InlineData("hey windows monitor 1 0", "monitor 1", 0)]
    [InlineData("hey windows third 3", "third", 3)]
    [InlineData("ei windows terceiro 10", "terceiro", 10)]
    [InlineData("ei windows terceiro 70", "terceiro", 70)]
    public void ShortForm_MatchesPattern(string text, string expectedTarget, int expectedValue)
    {
        var match = ShortPattern.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedTarget, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedValue, int.Parse(match.Groups[2].Value));
    }

    [Theory]
    [InlineData("MONITOR 1 50")]
    [InlineData("First 5")]
    [InlineData("PRIMEIRO 8")]
    public void ShortForm_CaseInsensitive(string text)
    {
        Assert.Matches(ShortPattern, text);
    }

    // =========================================================================
    // ALL FORM: "{all} {value}"
    // =========================================================================

    [Theory]
    [InlineData("hey windows both 5", "both", 5)]
    [InlineData("hey windows all 50", "all", 50)]
    [InlineData("ei windows ambos 3", "ambos", 3)]
    [InlineData("ei windows todos 80", "todos", 80)]
    public void AllForm_MatchesPattern(string text, string expectedAll, int expectedValue)
    {
        var match = AllPattern.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedAll, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedValue, int.Parse(match.Groups[2].Value));
    }

    // =========================================================================
    // LONG FORM 1: "{brightness} {value} {prep} {monitor}"
    // =========================================================================

    [Theory]
    [InlineData("hey windows brightness 5 on monitor 1", 5, "monitor 1")]
    [InlineData("hey windows brightness 3 in monitor 2", 3, "monitor 2")]
    [InlineData("hey windows brightness 10 on first", 10, "first")]
    [InlineData("ei windows brilho 5 no monitor 1", 5, "monitor 1")]
    [InlineData("ei windows brilho 3 do monitor 2", 3, "monitor 2")]
    [InlineData("ei windows brilho 8 em primeiro", 8, "primeiro")]
    [InlineData("ei windows brilho 5 no segundo", 5, "segundo")]
    public void Long1Form_MatchesPattern(string text, int expectedValue, string expectedTarget)
    {
        var match = Long1Pattern.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedValue, int.Parse(match.Groups[1].Value));
        Assert.Equal(expectedTarget, match.Groups[2].Value, StringComparer.OrdinalIgnoreCase);
    }

    // =========================================================================
    // LONG FORM 2: "{monitor} {brightness} {value}"
    // =========================================================================

    [Theory]
    [InlineData("hey windows monitor 1 brightness 5", "monitor 1", 5)]
    [InlineData("hey windows first brightness 10", "first", 10)]
    [InlineData("ei windows monitor 1 brilho 3", "monitor 1", 3)]
    [InlineData("ei windows primeiro brilho 5", "primeiro", 5)]
    [InlineData("ei windows segundo brilho 8", "segundo", 8)]
    public void Long2Form_MatchesPattern(string text, string expectedTarget, int expectedValue)
    {
        var match = Long2Pattern.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedTarget, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedValue, int.Parse(match.Groups[2].Value));
    }

    // =========================================================================
    // VERB-LED FORM (new): "{set} [{brightness} [{prep}]] {monitor} [{conn}] {value}"
    // =========================================================================

    [Theory]
    // en-US — target only
    [InlineData("set monitor 1 to 50",            "monitor 1", 50)]
    [InlineData("put first at 80",                "first",     80)]
    [InlineData("make second 70",                 "second",    70)]
    [InlineData("change third to 40",             "third",     40)]
    [InlineData("adjust monitor 2 to 25",         "monitor 2", 25)]
    // en-US — with brightness keyword
    [InlineData("set brightness on first to 50",  "first",     50)]
    [InlineData("adjust brightness on monitor 1 to 30", "monitor 1", 30)]
    // pt-BR — target only
    [InlineData("ajusta primeiro em 30",          "primeiro",  30)]
    [InlineData("coloca segundo em 80",           "segundo",   80)]
    [InlineData("define terceiro em 50",          "terceiro",  50)]
    [InlineData("deixa quarto em 20",             "quarto",    20)]
    [InlineData("muda monitor 1 para 100",        "monitor 1", 100)]
    public void VerbLedForm_MatchesPattern(string text, string expectedTarget, int expectedValue)
    {
        var match = VerbLedPattern.Match(text);
        Assert.True(match.Success, $"Expected match: \"{text}\"");
        Assert.Equal(expectedTarget, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedValue, int.Parse(match.Groups[2].Value));
    }

    // =========================================================================
    // NUMBER-FIRST FORM (new): "{value} {prep} {monitor}"
    // =========================================================================

    [Theory]
    [InlineData("50 on monitor 1",        50, "monitor 1")]
    [InlineData("80 in monitor 2",        80, "monitor 2")]
    [InlineData("30 no primeiro",         30, "primeiro")]
    [InlineData("50 no segundo",          50, "segundo")]
    [InlineData("80 em terceiro",         80, "terceiro")]
    [InlineData("100 no monitor 1",       100, "monitor 1")]
    public void NumberFirstForm_MatchesPattern(string text, int expectedValue, string expectedTarget)
    {
        var match = NumberFirstPattern.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedValue, int.Parse(match.Groups[1].Value));
        Assert.Equal(expectedTarget, match.Groups[2].Value, StringComparer.OrdinalIgnoreCase);
    }

    // =========================================================================
    // NO-TARGET FORM (new): "{brightness} [in|em] {value}" → all monitors
    // =========================================================================

    [Theory]
    // pt-BR
    [InlineData("brilho 30",         30)]
    [InlineData("brilho em 30",      30)]
    [InlineData("brilho 2",          2)]   // level — handler maps 2 → 20%
    [InlineData("luz 50",            50)]
    [InlineData("luz 2",             2)]
    [InlineData("luminosidade 80",   80)]
    [InlineData("luminosidade em 80",80)]
    // en-US
    [InlineData("brightness 30",     30)]
    [InlineData("brightness in 50",  50)]
    [InlineData("light 30",          30)]
    [InlineData("light in 20",       20)]
    public void NoTargetForm_MatchesPattern(string text, int expectedValue)
    {
        var match = NoTargetPattern.Match(text);
        Assert.True(match.Success, $"Expected match: \"{text}\"");
        Assert.Equal(expectedValue, int.Parse(match.Groups[1].Value));
    }

    [Fact]
    public void NoTargetForm_MustNotStealLong1Match()
    {
        // "brilho 5 no primeiro" is a target-specific utterance — Long1
        // claims it in dispatch order. The no-target pattern will ALSO
        // match the prefix "brilho 5", which is why it's tried last in
        // TryHandle.
        Assert.True(Long1Pattern.IsMatch("brilho 5 no primeiro"));
        Assert.True(NoTargetPattern.IsMatch("brilho 5 no primeiro"));
    }

    // =========================================================================
    // PATTERN REJECTION
    // =========================================================================

    [Theory]
    [InlineData("hey windows volume 5")]
    [InlineData("hello world")]
    [InlineData("")]
    public void UnrelatedText_DoesNotMatchAnyPattern(string text)
    {
        Assert.False(ShortPattern.IsMatch(text));
        Assert.False(AllPattern.IsMatch(text));
        Assert.False(Long1Pattern.IsMatch(text));
        Assert.False(Long2Pattern.IsMatch(text));
        Assert.False(VerbLedPattern.IsMatch(text));
        Assert.False(NumberFirstPattern.IsMatch(text));
        Assert.False(NoTargetPattern.IsMatch(text));
    }

    // =========================================================================
    // HANDLER METADATA
    // =========================================================================

    [Fact]
    public void Handler_NameIsBrightness()
    {
        var handler = new BrightnessCommandHandler(new MonitorControlService());
        Assert.Equal("Brightness", handler.Name);
    }

    [Fact]
    public void Handler_SupportsBothCultures()
    {
        var handler = new BrightnessCommandHandler(new MonitorControlService());
        var names = handler.SupportedCultures.Select(c => c.Name).ToList();
        Assert.Contains("en-US", names);
        Assert.Contains("pt-BR", names);
        Assert.Equal(2, handler.SupportedCultures.Count);
    }

    [Fact]
    public void BuildVocabulary_EnUs_DoesNotThrow()
    {
        var handler = new BrightnessCommandHandler(new MonitorControlService());
        Assert.NotNull(handler.BuildVocabulary(new CultureInfo("en-US")));
    }

    [Fact]
    public void BuildVocabulary_PtBr_DoesNotThrow()
    {
        var handler = new BrightnessCommandHandler(new MonitorControlService());
        Assert.NotNull(handler.BuildVocabulary(new CultureInfo("pt-BR")));
    }
}
