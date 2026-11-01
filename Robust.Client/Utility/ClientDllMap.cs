#if !WINDOWS
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Audio.OpenAL;
using Robust.Client.Audio.Codecs;
using SDL3;

namespace Robust.Client.Utility
{
    internal static class ClientDllMap
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            NativeLibrary.SetDllImportResolver(typeof(ClientDllMap).Assembly, (name, assembly, path) =>
            {
                if (name == "libEGL.dll")
                {
#if LINUX || FREEBSD
                    return NativeLibrary.Load("libEGL.so", assembly, path);
#endif
                }

                if (name == OpusNative.LibraryName)
                {
#if MACOS
                    const string opus = "libopus.0.dylib";
#else
                    const string opus = "libopus.so.0";
#endif
                    if (NativeLibrary.TryLoad(opus, assembly, path, out var handle))
                        return handle;
                }

                return IntPtr.Zero;
            });

#if MACOS
            OpenALLibraryNameContainer.OverridePath = "libopenal.1.dylib";
#endif
        }
    }
}
#endif
