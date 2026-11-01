using System;
using static Robust.Client.Audio.Codecs.OpusNative;

namespace Robust.Client.Audio.Codecs;

/// <summary>
/// Decodes Opus packets into 16-bit PCM with libopus, at 8000, 12000, 16000, 24000 or 48000 Hz regardless of the rate
/// the stream was encoded at. Not thread-safe.
/// </summary>
public sealed unsafe class OpusDecoder : IDisposable
{
    private IntPtr _state;

    public int SampleRate { get; }

    public int Channels { get; }

    /// <exception cref="OpusException">The sample rate or channel count is not supported.</exception>
    public OpusDecoder(int sampleRate, int channels)
    {
        int error;
        var state = opus_decoder_create(sampleRate, channels, &error);
        OpusException.ThrowIfError(error);

        _state = state;
        SampleRate = sampleRate;
        Channels = channels;
    }

    ~OpusDecoder()
    {
        Destroy();
    }

    /// <summary>
    /// Decodes one packet. An empty packet counts as lost, like <see cref="DecodeLost"/>.
    /// </summary>
    /// <param name="packet">The Opus packet.</param>
    /// <param name="pcm">Receives interleaved samples, room for <paramref name="frameSize"/> per channel.</param>
    /// <param name="frameSize">Samples per channel <paramref name="pcm"/> can take; up to 120 ms worth is useful.</param>
    /// <param name="fec">Rebuild the previous, lost packet from this packet's redundancy instead.</param>
    /// <returns>Samples decoded per channel.</returns>
    /// <exception cref="OpusException">The packet is malformed or longer than <paramref name="frameSize"/>.</exception>
    public int Decode(ReadOnlySpan<byte> packet, Span<short> pcm, int frameSize, bool fec = false)
    {
        CheckDisposed();
        CheckOutput(pcm, frameSize);

        fixed (byte* packetPtr = packet)
        fixed (short* pcmPtr = pcm)
        {
            return OpusException.ThrowIfError(
                opus_decode(_state, packetPtr, packet.Length, pcmPtr, frameSize, fec ? 1 : 0));
        }
    }

    /// <summary>
    /// Synthesises a stand-in for a packet that never arrived (packet loss concealment).
    /// </summary>
    /// <returns>Samples produced per channel.</returns>
    public int DecodeLost(Span<short> pcm, int frameSize)
    {
        CheckDisposed();
        CheckOutput(pcm, frameSize);

        fixed (short* pcmPtr = pcm)
        {
            return OpusException.ThrowIfError(opus_decode(_state, null, 0, pcmPtr, frameSize, 0));
        }
    }

    /// <summary>
    /// Forgets the previous audio, for starting an unrelated stream.
    /// </summary>
    public void Reset()
    {
        CheckDisposed();
        OpusException.ThrowIfError(DecoderCtlNoArg(_state, OPUS_RESET_STATE));
    }

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }

    private void CheckOutput(Span<short> pcm, int frameSize)
    {
        if (frameSize <= 0 || pcm.Length < (long) frameSize * Channels)
            throw new ArgumentException("There is no room for frameSize samples per channel.", nameof(pcm));
    }

    private void CheckDisposed()
    {
        ObjectDisposedException.ThrowIf(_state == IntPtr.Zero, this);
    }

    private void Destroy()
    {
        if (_state == IntPtr.Zero)
            return;

        opus_decoder_destroy(_state);
        _state = IntPtr.Zero;
    }
}
