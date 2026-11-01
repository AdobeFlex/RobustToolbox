using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Robust.Client.Audio.Codecs;

/// <summary>
/// Minimal libopus P/Invokes. The library ships with Robust.Natives.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static unsafe class OpusNative
{
    public const string LibraryName = "opus";

    public const int OPUS_OK = 0;

    public const int OPUS_APPLICATION_VOIP = 2048;
    public const int OPUS_APPLICATION_AUDIO = 2049;
    public const int OPUS_APPLICATION_RESTRICTED_LOWDELAY = 2051;

    public const int OPUS_SET_BITRATE_REQUEST = 4002;
    public const int OPUS_GET_BITRATE_REQUEST = 4003;
    public const int OPUS_SET_INBAND_FEC_REQUEST = 4012;
    public const int OPUS_GET_INBAND_FEC_REQUEST = 4013;
    public const int OPUS_SET_PACKET_LOSS_PERC_REQUEST = 4014;
    public const int OPUS_GET_PACKET_LOSS_PERC_REQUEST = 4015;
    public const int OPUS_RESET_STATE = 4028;

    [DllImport(LibraryName)]
    public static extern IntPtr opus_encoder_create(int fs, int channels, int application, int* error);

    [DllImport(LibraryName)]
    public static extern void opus_encoder_destroy(IntPtr st);

    [DllImport(LibraryName)]
    public static extern int opus_encode(IntPtr st, short* pcm, int frameSize, byte* data, int maxDataBytes);

    [DllImport(LibraryName)]
    public static extern IntPtr opus_decoder_create(int fs, int channels, int* error);

    [DllImport(LibraryName)]
    public static extern void opus_decoder_destroy(IntPtr st);

    [DllImport(LibraryName)]
    public static extern int opus_decode(IntPtr st, byte* data, int len, short* pcm, int frameSize, int decodeFec);

    [DllImport(LibraryName)]
    public static extern byte* opus_strerror(int error);

    // opus_encoder_ctl and opus_decoder_ctl are variadic, which P/Invoke can't express. Passing the variadic argument
    // like a regular one works on every ABI we ship except Apple silicon, which passes variadic arguments on the stack:
    // there, six padding arguments fill the remaining argument registers so the value lands where the callee reads it.

    private static readonly bool VariadicArgumentsOnStack =
        OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

    [DllImport(LibraryName, EntryPoint = "opus_encoder_ctl")]
    private static extern int EncoderCtl(IntPtr st, int request);

    [DllImport(LibraryName, EntryPoint = "opus_encoder_ctl")]
    private static extern int EncoderCtl(IntPtr st, int request, nint arg);

    [DllImport(LibraryName, EntryPoint = "opus_encoder_ctl")]
    private static extern int EncoderCtl(IntPtr st, int request, nint x2, nint x3, nint x4, nint x5, nint x6, nint x7, nint arg);

    [DllImport(LibraryName, EntryPoint = "opus_decoder_ctl")]
    private static extern int DecoderCtl(IntPtr st, int request);

    /// <summary>
    /// Calls a CTL that takes no argument, such as <see cref="OPUS_RESET_STATE"/>.
    /// </summary>
    public static int EncoderCtlNoArg(IntPtr st, int request)
    {
        return EncoderCtl(st, request);
    }

    /// <summary>
    /// Calls a setter CTL.
    /// </summary>
    public static int EncoderCtlSet(IntPtr st, int request, int value)
    {
        return VariadicArgumentsOnStack
            ? EncoderCtl(st, request, 0, 0, 0, 0, 0, 0, value)
            : EncoderCtl(st, request, value);
    }

    /// <summary>
    /// Calls a getter CTL.
    /// </summary>
    public static int EncoderCtlGet(IntPtr st, int request, out int value)
    {
        int result;
        fixed (int* ptr = &value)
        {
            result = VariadicArgumentsOnStack
                ? EncoderCtl(st, request, 0, 0, 0, 0, 0, 0, (nint) ptr)
                : EncoderCtl(st, request, (nint) ptr);
        }

        return result;
    }

    public static int DecoderCtlNoArg(IntPtr st, int request)
    {
        return DecoderCtl(st, request);
    }

    public static string ErrorString(int error)
    {
        return Marshal.PtrToStringAnsi((IntPtr) opus_strerror(error)) ?? $"Opus error {error}";
    }
}
