using System;

namespace Robust.Client.Audio.Codecs;

/// <summary>
/// Thrown when libopus reports an error, such as a malformed packet.
/// </summary>
public sealed class OpusException : Exception
{
    /// <summary>
    /// The libopus error code, a negative <c>OPUS_*</c> value.
    /// </summary>
    public int ErrorCode { get; }

    internal OpusException(int errorCode) : base(OpusNative.ErrorString(errorCode))
    {
        ErrorCode = errorCode;
    }

    internal static int ThrowIfError(int result)
    {
        if (result < OpusNative.OPUS_OK)
            throw new OpusException(result);

        return result;
    }
}
