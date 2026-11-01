using System;

namespace Robust.Client.Audio.Capture;

/// <summary>
/// Capture handed out where there is no audio device, such as the headless client.
/// </summary>
internal sealed class DummyAudioCapture : IAudioCapture
{
    public AudioCaptureState State => AudioCaptureState.Failed;

    public int SampleRate { get; }

    public bool Capturing => false;

    public event Action<AudioCaptureState>? StateChanged
    {
        add { }
        remove { }
    }

    public int AvailableSamples => 0;

    public DummyAudioCapture(int sampleRate)
    {
        SampleRate = sampleRate;
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public int Read(Span<short> buffer)
    {
        return 0;
    }

    public void Dispose()
    {
    }
}
