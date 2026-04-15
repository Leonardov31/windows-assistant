using NAudio.Wave;

namespace WindowsAssistant.Services;

/// <summary>
/// Captures microphone audio as raw PCM suitable for Vosk (16 kHz, 16-bit, mono).
///
/// A single capture is multiplexed to all Vosk recognizers by the caller.
/// Consumers receive <see cref="DataAvailable"/> events containing a freshly
/// allocated buffer sized to the exact number of bytes recorded.
/// </summary>
public sealed class AudioCaptureService : IDisposable
{
    /// <summary>Vosk models in this app are trained at 16 kHz.</summary>
    public const int SampleRate = 16000;

    private WaveInEvent? _waveIn;
    private bool _disposed;

    /// <summary>Fired whenever a chunk of audio is available. Buffer is owned by the handler.</summary>
    public event EventHandler<byte[]>? DataAvailable;

    /// <summary>Human-readable name of the currently selected input device (empty when not started).</summary>
    public string DeviceName { get; private set; } = "";

    /// <summary>Returns the list of available input devices for diagnostics.</summary>
    public static IReadOnlyList<string> EnumerateDevices()
    {
        var list = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            try
            {
                list.Add($"[{i}] {WaveInEvent.GetCapabilities(i).ProductName}");
            }
            catch
            {
                list.Add($"[{i}] <unavailable>");
            }
        }
        return list;
    }

    /// <summary>
    /// Starts capture from device 0 (the first Windows-enumerated input device).
    /// No-op if already started. Throws <see cref="InvalidOperationException"/>
    /// when no input devices are present on the system.
    /// </summary>
    public void Start()
    {
        if (_waveIn is not null) return;

        if (WaveInEvent.DeviceCount == 0)
            throw new InvalidOperationException(
                "No audio input devices found. Plug in a microphone and restart the app.");

        _waveIn = new WaveInEvent
        {
            DeviceNumber       = 0,
            WaveFormat         = new WaveFormat(SampleRate, bits: 16, channels: 1),
            BufferMilliseconds = 100,
        };
        DeviceName = WaveInEvent.GetCapabilities(0).ProductName;
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
    }

    public void Stop()
    {
        if (_waveIn is null) return;
        try
        {
            _waveIn.StopRecording();
        }
        catch
        {
            // Swallow — StopRecording can throw if device was disconnected
        }
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.Dispose();
        _waveIn = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0) return;

        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
        DataAvailable?.Invoke(this, buffer);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
