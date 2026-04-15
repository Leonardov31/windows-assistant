using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="LanguageSetupService"/>:
///   - Culture detection (installed vs. missing)
///   - Required cultures list
///   - Consistency between installed + missing = all required
/// </summary>
public class LanguageSetupServiceTests
{
    // -------------------------------------------------------------------------
    // GetInstalledCultures — returns only recognized cultures
    // -------------------------------------------------------------------------

    [Fact]
    public void GetInstalledCultures_ReturnsOnlyRequiredCultures()
    {
        var installed = LanguageSetupService.GetInstalledCultures();

        // Should only contain cultures from the required set (en-US, pt-BR)
        foreach (var culture in installed)
        {
            Assert.True(
                culture == "en-US" || culture == "pt-BR",
                $"Unexpected culture: {culture}");
        }
    }

    [Fact]
    public void GetInstalledCultures_ReturnsNonNullList()
    {
        var installed = LanguageSetupService.GetInstalledCultures();
        Assert.NotNull(installed);
    }

    // -------------------------------------------------------------------------
    // GetMissingCultures — complement of installed
    // -------------------------------------------------------------------------

    [Fact]
    public void GetMissingCultures_ReturnsNonNullList()
    {
        var missing = LanguageSetupService.GetMissingCultures();
        Assert.NotNull(missing);
    }

    [Fact]
    public void GetMissingCultures_ContainsOnlyRequiredCultures()
    {
        var missing = LanguageSetupService.GetMissingCultures();

        foreach (var culture in missing)
        {
            Assert.True(
                culture == "en-US" || culture == "pt-BR",
                $"Unexpected missing culture: {culture}");
        }
    }

    // -------------------------------------------------------------------------
    // Installed + Missing = All Required
    // -------------------------------------------------------------------------

    [Fact]
    public void InstalledAndMissing_CoverAllRequired()
    {
        var installed = LanguageSetupService.GetInstalledCultures();
        var missing = LanguageSetupService.GetMissingCultures();

        var all = installed.Concat(missing).OrderBy(c => c).ToList();

        Assert.Contains("en-US", all);
        Assert.Contains("pt-BR", all);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void InstalledAndMissing_DoNotOverlap()
    {
        var installed = LanguageSetupService.GetInstalledCultures().ToHashSet();
        var missing = LanguageSetupService.GetMissingCultures();

        foreach (var culture in missing)
        {
            Assert.DoesNotContain(culture, installed);
        }
    }
}
