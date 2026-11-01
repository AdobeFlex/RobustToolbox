using NUnit.Framework;
using Robust.Client.Audio.Codecs;

namespace Robust.Client.Tests.Audio;

[TestFixture, Parallelizable]
[TestOf(typeof(OpusEncoder))]
[TestOf(typeof(OpusDecoder))]
public sealed class OpusCodecTest
{
    private const int SampleRate = 48000;
    private const int FrameSize = SampleRate / 50;

    [SetUp]
    public void RequireLibopus()
    {
        try
        {
            new OpusDecoder(SampleRate, 1).Dispose();
        }
        catch (DllNotFoundException)
        {
            Assert.Ignore("libopus is not available on this machine.");
        }
    }

    private static short[] Sine(int frames, float frequency = 440f, float amplitude = 8000f)
    {
        var samples = new short[frames * FrameSize];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short) (amplitude * MathF.Sin(2f * MathF.PI * frequency * i / SampleRate));
        }

        return samples;
    }

    private static double Rms(ReadOnlySpan<short> samples)
    {
        double sum = 0;
        foreach (var sample in samples)
        {
            sum += (double) sample * sample;
        }

        return Math.Sqrt(sum / samples.Length);
    }

    [Test]
    public void RoundTripKeepsTheSignal()
    {
        const int frames = 25;
        var input = Sine(frames);
        var output = new short[input.Length];

        using var encoder = new OpusEncoder(SampleRate, 1);
        using var decoder = new OpusDecoder(SampleRate, 1);
        var packet = new byte[OpusEncoder.MaxPacketSize];

        for (var i = 0; i < frames; i++)
        {
            var length = encoder.Encode(input.AsSpan(i * FrameSize, FrameSize), FrameSize, packet);
            var decoded = decoder.Decode(packet.AsSpan(0, length), output.AsSpan(i * FrameSize, FrameSize), FrameSize);
            Assert.That(decoded, Is.EqualTo(FrameSize));
        }

        // Skip the start, where the codec is still settling.
        var inputRms = Rms(input.AsSpan(10 * FrameSize));
        var outputRms = Rms(output.AsSpan(10 * FrameSize));
        Assert.That(outputRms, Is.InRange(inputRms * 0.7, inputRms * 1.3));
    }

    [Test]
    public void DecodesAtAnotherRate()
    {
        var input = Sine(1);
        var packet = new byte[OpusEncoder.MaxPacketSize];
        var output = new short[FrameSize / 3];

        using var encoder = new OpusEncoder(SampleRate, 1);
        using var decoder = new OpusDecoder(16000, 1);
        var length = encoder.Encode(input, FrameSize, packet);

        Assert.That(decoder.Decode(packet.AsSpan(0, length), output, output.Length), Is.EqualTo(output.Length));
    }

    [Test]
    public void SettingsReadBack()
    {
        using var encoder = new OpusEncoder(SampleRate, 1)
        {
            Bitrate = 24000,
            InbandFec = true,
            PacketLossPercent = 10,
        };

        Assert.Multiple(() =>
        {
            Assert.That(encoder.Bitrate, Is.EqualTo(24000));
            Assert.That(encoder.InbandFec, Is.True);
            Assert.That(encoder.PacketLossPercent, Is.EqualTo(10));
        });

        encoder.Reset();
    }

    [Test]
    public void LostPacketsAreConcealed()
    {
        using var decoder = new OpusDecoder(SampleRate, 1);
        var output = new short[FrameSize];

        Assert.Multiple(() =>
        {
            Assert.That(decoder.DecodeLost(output, FrameSize), Is.EqualTo(FrameSize));
            Assert.That(decoder.Decode(ReadOnlySpan<byte>.Empty, output, FrameSize), Is.EqualTo(FrameSize));
        });
    }

    [Test]
    public void MalformedPacketThrows()
    {
        using var decoder = new OpusDecoder(SampleRate, 1);
        var output = new short[FrameSize];

        // Code 3 packets need a frame count byte after the TOC byte.
        Assert.That(() => decoder.Decode(new byte[] { 0x03 }, output, FrameSize), Throws.TypeOf<OpusException>());
    }

    [Test]
    public void ShortBuffersAreRejected()
    {
        using var encoder = new OpusEncoder(SampleRate, 2);
        using var decoder = new OpusDecoder(SampleRate, 2);
        var packet = new byte[OpusEncoder.MaxPacketSize];

        Assert.Multiple(() =>
        {
            Assert.That(() => encoder.Encode(new short[FrameSize], FrameSize, packet), Throws.ArgumentException);
            Assert.That(() => decoder.DecodeLost(new short[FrameSize], FrameSize), Throws.ArgumentException);
        });
    }

    [Test]
    public void UnsupportedSampleRateThrows()
    {
        Assert.That(() => new OpusEncoder(44100, 1), Throws.TypeOf<OpusException>());
    }

    [Test]
    public void DisposedCodecThrows()
    {
        var encoder = new OpusEncoder(SampleRate, 1);
        encoder.Dispose();

        Assert.That(() => encoder.Bitrate = 16000, Throws.TypeOf<ObjectDisposedException>());
    }
}
