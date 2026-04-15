using WindowsAssistant.Commands;
using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="CommandEventArgs"/> record:
///   - Property binding
///   - Record equality
/// </summary>
public class CommandEventArgsTests
{
    [Fact]
    public void CommandEventArgs_StoresAllProperties()
    {
        var result = new CommandResult(true, "OK");
        var args = new CommandEventArgs(
            HandlerName: "Brightness",
            RecognizedText: "hey windows brightness 5 in monitor 1",
            Confidence: 0.85f,
            Result: result);

        Assert.Equal("Brightness", args.HandlerName);
        Assert.Equal("hey windows brightness 5 in monitor 1", args.RecognizedText);
        Assert.Equal(0.85f, args.Confidence);
        Assert.Same(result, args.Result);
    }

    [Fact]
    public void CommandEventArgs_EqualRecordsAreEqual()
    {
        var result = new CommandResult(true, "OK");
        var a = new CommandEventArgs("Brightness", "text", 0.9f, result);
        var b = new CommandEventArgs("Brightness", "text", 0.9f, result);

        Assert.Equal(a, b);
    }

    [Fact]
    public void CommandEventArgs_DifferentConfidence_NotEqual()
    {
        var result = new CommandResult(true, "OK");
        var a = new CommandEventArgs("Brightness", "text", 0.9f, result);
        var b = new CommandEventArgs("Brightness", "text", 0.5f, result);

        Assert.NotEqual(a, b);
    }
}
