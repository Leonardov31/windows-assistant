using System.Speech.Recognition;

namespace WindowsAssistant.Commands;

/// <summary>
/// Contract for a voice command module.
/// Implement this interface to add new voice-activated features.
/// </summary>
public interface ICommandHandler
{
    /// <summary>Display name shown in logs and tray notifications.</summary>
    string Name { get; }

    /// <summary>
    /// Returns the grammar fragment for this command (without the wake phrase).
    /// Called once at startup to build the combined recognition grammar.
    /// </summary>
    GrammarBuilder BuildGrammar();

    /// <summary>
    /// Tries to handle a recognition result.
    /// Returns a <see cref="CommandResult"/> if the result belongs to this handler,
    /// or <c>null</c> if the result should be passed to the next handler.
    /// </summary>
    CommandResult? TryHandle(RecognitionResult result);
}
