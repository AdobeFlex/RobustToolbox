using System;
using System.Collections.Generic;

namespace Robust.Client.Audio.Capture;

/// <summary>
/// The microphone hardware behind <see cref="AudioCaptureManager"/>, kept apart so the consent logic can be tested
/// without a real device.
/// </summary>
internal interface IAudioCaptureBackend
{
    IReadOnlyList<string> GetDevices();

    /// <summary>
    /// Opens a microphone for mono signed 16-bit capture.
    /// </summary>
    /// <returns>null if the device could not be opened.</returns>
    ICaptureDevice? Open(string device, int sampleRate);
}

/// <summary>
/// An open microphone. Disposing it closes the device.
/// </summary>
internal interface ICaptureDevice : IDisposable
{
    int AvailableSamples { get; }

    void Start();

    void Stop();

    /// <summary>
    /// Reads exactly <paramref name="buffer"/>.Length samples, which must not exceed <see cref="AvailableSamples"/>.
    /// </summary>
    void Read(Span<short> buffer);
}
