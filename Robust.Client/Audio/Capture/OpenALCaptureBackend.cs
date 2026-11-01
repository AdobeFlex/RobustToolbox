using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using Robust.Shared.Log;

namespace Robust.Client.Audio.Capture;

internal sealed class OpenALCaptureBackend : IAudioCaptureBackend
{
    private readonly ISawmill _sawmill;

    public OpenALCaptureBackend(ISawmill sawmill)
    {
        _sawmill = sawmill;
    }

    public IReadOnlyList<string> GetDevices()
    {
        if (!ALC.IsExtensionPresent(ALDevice.Null, "ALC_EXT_CAPTURE"))
            return [];

        return new List<string>(ALC.GetStringList(GetEnumerationStringList.CaptureDeviceSpecifier));
    }

    public ICaptureDevice? Open(string device, int sampleRate)
    {
        // Half a second of headroom between reads.
        var handle = ALC.CaptureOpenDevice(device, sampleRate, ALFormat.Mono16, sampleRate / 2);
        if (handle != ALCaptureDevice.Null)
            return new CaptureDevice(handle);

        _sawmill.Warning("Unable to open capture device '{0}'.", device);
        return null;
    }

    private sealed class CaptureDevice : ICaptureDevice
    {
        private readonly ALCaptureDevice _handle;

        public CaptureDevice(ALCaptureDevice handle)
        {
            _handle = handle;
        }

        public int AvailableSamples => ALC.GetInteger(_handle, AlcGetInteger.CaptureSamples);

        public void Start()
        {
            ALC.CaptureStart(_handle);
        }

        public void Stop()
        {
            ALC.CaptureStop(_handle);
        }

        public unsafe void Read(Span<short> buffer)
        {
            fixed (short* ptr = buffer)
            {
                ALC.CaptureSamples(_handle, ptr, buffer.Length);
            }
        }

        public void Dispose()
        {
            ALC.CaptureCloseDevice(_handle);
        }
    }
}
