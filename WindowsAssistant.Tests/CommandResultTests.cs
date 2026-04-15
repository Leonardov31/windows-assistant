using WindowsAssistant.Commands;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="CommandResult"/> record type.
/// </summary>
public class CommandResultTests
{
    [Fact]
    public void SuccessResult_HasCorrectProperties()
    {
        var result = new CommandResult(true, "Monitor 1 → 50%");

        Assert.True(result.Success);
        Assert.Equal("Monitor 1 → 50%", result.Message);
    }

    [Fact]
    public void FailureResult_HasCorrectProperties()
    {
        var result = new CommandResult(false, "Could not set brightness");

        Assert.False(result.Success);
        Assert.Equal("Could not set brightness", result.Message);
    }

    [Fact]
    public void EqualResults_AreEqual()
    {
        var a = new CommandResult(true, "OK");
        var b = new CommandResult(true, "OK");

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentResults_AreNotEqual()
    {
        var a = new CommandResult(true, "OK");
        var b = new CommandResult(false, "OK");

        Assert.NotEqual(a, b);
    }
}
