namespace WindowsAssistant.Commands;

/// <summary>
/// Engine-agnostic recognition payload passed to <see cref="ICommandHandler.TryHandle"/>.
/// Decouples handlers from any specific speech engine type.
/// </summary>
/// <param name="Text">Full recognized utterance (including wake phrase, if any).</param>
/// <param name="Confidence">Confidence score in the range [0.0, 1.0].</param>
public readonly record struct RecognitionOutput(string Text, float Confidence);
