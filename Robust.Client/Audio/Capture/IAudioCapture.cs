using System;
using Robust.Shared;

namespace Robust.Client.Audio.Capture;

/// <summary>
/// Where a microphone request stands. Only <see cref="Open"/> captures anything.
/// </summary>
public enum AudioCaptureState : byte
{
    /// <summary>
    /// The player has not answered the engine prompt yet.
    /// </summary>
    Pending,

    /// <summary>
    /// The device is open; <see cref="IAudioCapture.Start"/> begins recording.
    /// </summary>
    Open,

    /// <summary>
    /// The player refused microphone access for this server.
    /// </summary>
    Denied,

    /// <summary>
    /// Access was allowed but the device could not be opened.
    /// </summary>
    Failed,

    /// <summary>
    /// The capture was disposed, or closed by the engine on disconnect.
    /// </summary>
    Closed,
}

/// <summary>
/// A microphone handle from <see cref="IAudioManager.RequestCapture"/>. Audio is mono signed 16-bit PCM.
/// The engine shows an indicator while any capture is recording.
/// </summary>
[NotContentImplementable]
public interface IAudioCapture : IDisposable
{
    AudioCaptureState State { get; }

    int SampleRate { get; }

    /// <summary>
    /// Whether the microphone is currently recording.
    /// </summary>
    bool Capturing { get; }

    /// <summary>
    /// Raised on the main thread whenever <see cref="State"/> changes.
    /// </summary>
    event Action<AudioCaptureState>? StateChanged;

    /// <summary>
    /// Starts recording. Samples left over from an earlier recording are discarded. Does nothing unless open.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops recording. Samples recorded so far stay readable.
    /// </summary>
    void Stop();

    /// <summary>
    /// How many recorded samples are waiting to be read.
    /// </summary>
    int AvailableSamples { get; }

    /// <summary>
    /// Reads up to <paramref name="buffer"/>.Length recorded samples.
    /// </summary>
    /// <returns>The number of samples written into <paramref name="buffer"/>.</returns>
    int Read(Span<short> buffer);
}
