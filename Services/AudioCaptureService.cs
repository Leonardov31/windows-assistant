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

    /// <summary>
    /// Starts capture from the default input device. No-op if already started.
    /// </summary>
    public void Start()
    {
        if (_waveIn is not null) return;

        _waveIn = new WaveInEvent
        {
            WaveFormat    = new WaveFormat(SampleRate, bits: 16, channels: 1),
            BufferMilliseconds = 100,
        };
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
