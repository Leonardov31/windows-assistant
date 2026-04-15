using System.Media;

namespace WindowsAssistant.Infrastructure;

/// <summary>
/// Audio cues for voice-assistant state transitions. Uses built-in Windows
/// system sounds so there are no WAV resources to ship and the user's sound
/// scheme / mute setting is automatically respected.
/// </summary>
internal static class ChimeService
{
    /// <summary>
    /// Plays a short chime when the wake phrase is detected, confirming that
    /// the assistant is now listening for a command. Fire-and-forget; never
    /// blocks the caller. No-op if system sounds are disabled.
    /// </summary>
    public static void PlayWakeChime()
    {
        try { SystemSounds.Asterisk.Play(); }
        catch { /* system sounds unavailable; no-op */ }
    }
}
