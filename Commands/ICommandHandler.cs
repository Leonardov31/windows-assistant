using System.Globalization;
using System.Speech.Recognition;

namespace WindowsAssistant.Commands;

/// <summary>
/// Contract for a voice command module.
/// Implement this interface to add new voice-activated features.
///
/// Each handler declares which cultures it supports and provides a
/// grammar fragment per culture. The voice listener creates one
/// recognition engine per culture and merges the grammars automatically.
/// </summary>
public interface ICommandHandler
{
    /// <summary>Display name shown in logs and tray notifications.</summary>
    string Name { get; }

    /// <summary>Cultures this handler can recognise (e.g. en-US, pt-BR).</summary>
    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <summary>
    /// Returns the grammar fragment for the given culture (without the wake phrase).
    /// Called once per culture at startup.
    /// </summary>
    GrammarBuilder BuildGrammar(CultureInfo culture);

    /// <summary>
    /// Tries to handle a recognition result (language-agnostic text parsing).
    /// Returns a <see cref="CommandResult"/> if the result belongs to this handler,
    /// or <c>null</c> if the result should be passed to the next handler.
    /// </summary>
    CommandResult? TryHandle(RecognitionResult result);
}
