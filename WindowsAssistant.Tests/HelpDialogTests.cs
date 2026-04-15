using WindowsAssistant.UI;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="HelpDialog"/>:
///   - Dialog creation and properties
///   - Help content completeness
/// </summary>
public class HelpDialogTests : IDisposable
{
    private readonly HelpDialog _dialog = new();

    [Fact]
    public void HelpDialog_HasCorrectTitle()
    {
        Assert.Equal("Windows Assistant — Help", _dialog.Text);
    }

    [Fact]
    public void HelpDialog_HasFixedDialogBorderStyle()
    {
        Assert.Equal(FormBorderStyle.FixedDialog, _dialog.FormBorderStyle);
    }

    [Fact]
    public void HelpDialog_MaximizeAndMinimizeDisabled()
    {
        Assert.False(_dialog.MaximizeBox);
        Assert.False(_dialog.MinimizeBox);
    }

    [Fact]
    public void HelpDialog_NotShownInTaskbar()
    {
        Assert.False(_dialog.ShowInTaskbar);
    }

    [Fact]
    public void HelpDialog_CenteredOnScreen()
    {
        Assert.Equal(FormStartPosition.CenterScreen, _dialog.StartPosition);
    }

    [Fact]
    public void HelpDialog_ContainsTextBox()
    {
        Assert.Single(_dialog.Controls);
        Assert.IsType<TextBox>(_dialog.Controls[0]);
    }

    [Fact]
    public void HelpDialog_TextBoxIsReadOnly()
    {
        var textBox = (TextBox)_dialog.Controls[0];
        Assert.True(textBox.ReadOnly);
    }

    [Fact]
    public void HelpDialog_TextBoxHasDarkTheme()
    {
        var textBox = (TextBox)_dialog.Controls[0];
        Assert.Equal(Color.FromArgb(30, 30, 30), textBox.BackColor);
        Assert.Equal(Color.FromArgb(220, 220, 220), textBox.ForeColor);
    }

    // -------------------------------------------------------------------------
    // Help content must document all features
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Hey Windows")]
    [InlineData("Ei Windows")]
    [InlineData("brightness")]
    [InlineData("brilho")]
    [InlineData("first")]
    [InlineData("primeiro")]
    [InlineData("both")]
    [InlineData("ambos")]
    [InlineData("monitor")]
    [InlineData("Speech speed")]
    [InlineData("Slow")]
    [InlineData("Normal")]
    [InlineData("Fast")]
    [InlineData("DDC/CI")]
    [InlineData("Language Setup")]
    [InlineData("administrator")]
    [InlineData("MONITOR POWER")]
    [InlineData("turn off")]
    [InlineData("desligar")]
    [InlineData("ligar")]
    [InlineData("ativar")]
    [InlineData("desativar")]
    [InlineData("Long form")]
    [InlineData("Short form")]
    public void HelpContent_ContainsExpectedSection(string keyword)
    {
        var textBox = (TextBox)_dialog.Controls[0];
        Assert.Contains(keyword, textBox.Text, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _dialog.Dispose();
}
