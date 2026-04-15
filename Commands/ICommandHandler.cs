using System.Globalization;

namespace WindowsAssistant.Commands;

/// <summary>
/// Contract for a voice command module.
/// Implement this interface to add new voice-activated features.
///
/// Each handler declares which cultures it supports and the list of words
/// it can recognise per culture. The voice listener merges all handler
/// vocabularies into a single Vosk grammar per culture.
/// </summary>
public interface ICommandHandler
{
    /// <summary>Display name shown in logs and tray notifications.</summary>
    string Name { get; }

    /// <summary>Cultures this handler can recognise (e.g. en-US, pt-BR).</summary>
    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <summary>
    /// Returns the flat list of words this handler can recognise in the given
    /// culture (without the wake phrase). Called once per culture at startup.
    /// Duplicates across handlers are fine — the listener deduplicates.
    /// </summary>
    IReadOnlyList<string> BuildVocabulary(CultureInfo culture);

    /// <summary>
    /// Tries to handle a recognized utterance (language-agnostic text parsing).
    /// Returns a <see cref="CommandResult"/> if the utterance belongs to this handler,
    /// or <c>null</c> if it should be passed to the next handler.
    /// </summary>
    CommandResult? TryHandle(RecognitionOutput output);
}
