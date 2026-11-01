using System;

namespace Robust.Client.Audio.Capture;

/// <summary>
/// A microphone request handed to content. <see cref="AudioCaptureManager"/> opens a device for it only once the
/// player allowed the server, on the microphone they picked.
/// </summary>
internal sealed class AudioCapture : IAudioCapture
{
    private readonly AudioCaptureManager _manager;
    private readonly IAudioCaptureBackend _backend;
    private ICaptureDevice? _device;

    public AudioCaptureState State { get; private set; } = AudioCaptureState.Pending;

    public int SampleRate { get; }

    public bool Capturing { get; private set; }

    public event Action<AudioCaptureState>? StateChanged;

    public AudioCapture(AudioCaptureManager manager, IAudioCaptureBackend backend, int sampleRate)
    {
        _manager = manager;
        _backend = backend;
        SampleRate = sampleRate;
    }

    public int AvailableSamples => _device?.AvailableSamples ?? 0;

    internal void Open(string device)
    {
        if (State != AudioCaptureState.Pending)
            return;

        _device = _backend.Open(device, SampleRate);
        SetState(_device != null ? AudioCaptureState.Open : AudioCaptureState.Failed);
    }

    /// <summary>
    /// Moves an open capture to another microphone, recording on if it was.
    /// </summary>
    internal void Reopen(string device)
    {
        if (State != AudioCaptureState.Open)
            return;

        var wasCapturing = Capturing;
        CloseDevice();

        _device = device.Length > 0 ? _backend.Open(device, SampleRate) : null;
        if (_device == null)
        {
            SetState(AudioCaptureState.Failed);
            return;
        }

        if (!wasCapturing)
            return;

        _device.Start();
        Capturing = true;
    }

    internal void Deny()
    {
        if (State == AudioCaptureState.Pending)
            SetState(AudioCaptureState.Denied);
    }

    public void Start()
    {
        if (_device == null || Capturing)
            return;

        DiscardSamples();
        _device.Start();
        Capturing = true;
    }

    public void Stop()
    {
        if (!Capturing)
            return;

        _device?.Stop();
        Capturing = false;
    }

    public int Read(Span<short> buffer)
    {
        var count = Math.Min(buffer.Length, AvailableSamples);
        if (count <= 0)
            return 0;

        _device!.Read(buffer[..count]);
        return count;
    }

    public void Dispose()
    {
        Close();
        _manager.Remove(this);
    }

    /// <summary>
    /// Closes the device without unregistering, for the manager's own cleanup.
    /// </summary>
    internal void Close()
    {
        if (State == AudioCaptureState.Closed)
            return;

        CloseDevice();
        SetState(AudioCaptureState.Closed);
    }

    private void CloseDevice()
    {
        Stop();
        _device?.Dispose();
        _device = null;
    }

    private void DiscardSamples()
    {
        Span<short> scratch = stackalloc short[512];
        while (Read(scratch) > 0)
        {
        }
    }

    private void SetState(AudioCaptureState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
