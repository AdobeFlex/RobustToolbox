using System;
using static Robust.Client.Audio.Codecs.OpusNative;

namespace Robust.Client.Audio.Codecs;

/// <summary>
/// What an Opus encoder tunes itself for.
/// </summary>
public enum OpusApplication : byte
{
    /// <summary>
    /// Speech: favours intelligibility at low bitrates.
    /// </summary>
    Voip,

    /// <summary>
    /// Music and other non-speech audio.
    /// </summary>
    Audio,

    /// <summary>
    /// Lowest latency, at the cost of quality.
    /// </summary>
    LowDelay,
}

/// <summary>
/// Encodes 16-bit PCM into Opus packets with libopus. Not thread-safe.
/// The sample rate must be 8000, 12000, 16000, 24000 or 48000 Hz, and a frame 2.5, 5, 10, 20, 40 or 60 ms of audio.
/// </summary>
public sealed unsafe class OpusEncoder : IDisposable
{
    /// <summary>
    /// Largest packet Opus produces for a single frame.
    /// </summary>
    public const int MaxPacketSize = 1275;

    private IntPtr _state;

    public int SampleRate { get; }

    public int Channels { get; }

    /// <exception cref="OpusException">The sample rate or channel count is not supported.</exception>
    public OpusEncoder(int sampleRate, int channels, OpusApplication application = OpusApplication.Voip)
    {
        var mode = application switch
        {
            OpusApplication.Audio => OPUS_APPLICATION_AUDIO,
            OpusApplication.LowDelay => OPUS_APPLICATION_RESTRICTED_LOWDELAY,
            _ => OPUS_APPLICATION_VOIP,
        };

        int error;
        var state = opus_encoder_create(sampleRate, channels, mode, &error);
        OpusException.ThrowIfError(error);

        _state = state;
        SampleRate = sampleRate;
        Channels = channels;
    }

    ~OpusEncoder()
    {
        Destroy();
    }

    /// <summary>
    /// Target bitrate in bits per second.
    /// </summary>
    public int Bitrate
    {
        get => Get(OPUS_GET_BITRATE_REQUEST);
        set => Set(OPUS_SET_BITRATE_REQUEST, value);
    }

    /// <summary>
    /// Embeds redundancy so the decoder can rebuild a lost packet from the next one.
    /// </summary>
    public bool InbandFec
    {
        get => Get(OPUS_GET_INBAND_FEC_REQUEST) != 0;
        set => Set(OPUS_SET_INBAND_FEC_REQUEST, value ? 1 : 0);
    }

    /// <summary>
    /// Expected packet loss in percent, which tunes how much redundancy <see cref="InbandFec"/> adds.
    /// </summary>
    public int PacketLossPercent
    {
        get => Get(OPUS_GET_PACKET_LOSS_PERC_REQUEST);
        set => Set(OPUS_SET_PACKET_LOSS_PERC_REQUEST, value);
    }

    /// <summary>
    /// Encodes one frame.
    /// </summary>
    /// <param name="pcm">Interleaved samples, at least <paramref name="frameSize"/> per channel.</param>
    /// <param name="frameSize">Samples per channel in the frame.</param>
    /// <param name="output">Receives the packet; <see cref="MaxPacketSize"/> bytes always suffice.</param>
    /// <returns>The packet length in bytes.</returns>
    /// <exception cref="OpusException">The frame size is invalid or <paramref name="output"/> is too small.</exception>
    public int Encode(ReadOnlySpan<short> pcm, int frameSize, Span<byte> output)
    {
        CheckDisposed();

        if (frameSize <= 0 || pcm.Length < (long) frameSize * Channels)
            throw new ArgumentException("Fewer than frameSize samples per channel were given.", nameof(pcm));

        fixed (short* pcmPtr = pcm)
        fixed (byte* outputPtr = output)
        {
            return OpusException.ThrowIfError(opus_encode(_state, pcmPtr, frameSize, outputPtr, output.Length));
        }
    }

    /// <summary>
    /// Forgets the previous audio, for starting an unrelated stream.
    /// </summary>
    public void Reset()
    {
        CheckDisposed();
        OpusException.ThrowIfError(EncoderCtlNoArg(_state, OPUS_RESET_STATE));
    }

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }

    private int Get(int request)
    {
        CheckDisposed();
        OpusException.ThrowIfError(EncoderCtlGet(_state, request, out var value));
        return value;
    }

    private void Set(int request, int value)
    {
        CheckDisposed();
        OpusException.ThrowIfError(EncoderCtlSet(_state, request, value));
    }

    private void CheckDisposed()
    {
        ObjectDisposedException.ThrowIf(_state == IntPtr.Zero, this);
    }

    private void Destroy()
    {
        if (_state == IntPtr.Zero)
            return;

        opus_encoder_destroy(_state);
        _state = IntPtr.Zero;
    }
}
