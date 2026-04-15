namespace WindowsAssistant.Commands;

/// <summary>
/// Result returned by a command handler after execution.
/// </summary>
/// <param name="Success">Whether the command executed successfully.</param>
/// <param name="Message">Human-readable message shown in tray notification.</param>
public sealed record CommandResult(bool Success, string Message);
