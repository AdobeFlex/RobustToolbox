using System;

namespace Robust.Shared.Audio.Sources;

/// <summary>
/// An audio source that plays PCM buffers queued by the caller, for audio generated at runtime (MIDI, voice).
/// Buffers are addressed by index, from 0 up to the buffer count the source was created with.
/// </summary>
[NotContentImplementable]
public interface IBufferedAudioSource : IAudioSource
{
    int SampleRate { get; set; }

    /// <summary>
    /// How many queued buffers have finished playing and can be reclaimed with <see cref="GetBuffersProcessed"/>.
    /// </summary>
    int GetNumberOfBuffersProcessed();

    /// <summary>
    /// Unqueues finished buffers and writes their indices into <paramref name="handles"/>.
    /// </summary>
    void GetBuffersProcessed(Span<int> handles);

    void WriteBuffer(int handle, ReadOnlySpan<ushort> data);

    void WriteBuffer(int handle, ReadOnlySpan<float> data);

    /// <summary>
    /// Uploads mono signed 16-bit samples to a buffer. Every sample in <paramref name="samples"/> is uploaded.
    /// </summary>
    void WriteBuffer(int handle, ReadOnlySpan<short> samples);

    void QueueBuffers(ReadOnlySpan<int> handles);

    void EmptyBuffers();
}
