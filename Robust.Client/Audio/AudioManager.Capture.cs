using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Robust.Client.Audio.Capture;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.ContentPack;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Robust.Client.Audio;

/// <summary>
/// Microphone capture. Content may ask for the microphone, but only the player can grant it and pick the device,
/// through an engine prompt; the engine also draws an indicator over all UI while anything records,
/// unless the player hid it with <see cref="CVars.AudioCaptureIndicator"/>.
/// </summary>
internal sealed partial class AudioManager
{
    [Shared.IoC.Dependency] private IUserInterfaceManager _ui = default!;
    [Shared.IoC.Dependency] private IClientNetManager _net = default!;

    private const string CaptureConsentFile = "microphone_consent.json";

    private AudioCaptureManager? _capture;
    private Font? _captureIndicatorFont;
    private bool _showCaptureIndicator = true;

    public IReadOnlyList<string> GetCaptureDevices()
    {
        return GetCaptureManager().GetDevices();
    }

    public IAudioCapture RequestCapture(int sampleRate = 48000)
    {
        DebugTools.Assert(IsMainThread(), "Microphone capture must be requested from the main thread.");
        return GetCaptureManager().Request(GetCaptureServer(), sampleRate);
    }

    public void ResetCaptureConsent()
    {
        DebugTools.Assert(IsMainThread(), "Microphone consent must be reset from the main thread.");
        GetCaptureManager().Reset(GetCaptureServer());
    }

    private AudioCaptureManager GetCaptureManager()
    {
        if (_capture != null)
            return _capture;

        var store = new AudioCaptureConsentStore(GetCaptureConsentPath(), _logMan.GetSawmill("audio.capture"));
        _capture = new AudioCaptureManager(new OpenALCaptureBackend(OpenALSawmill),
            new AudioCaptureConsentPrompt(),
            store,
            _cfg.GetCVar(CVars.AudioCaptureDevice));

        _capture.DeviceChosen += OnCaptureDeviceChosen;
        _net.ClientConnectStateChanged += OnCaptureConnectionChanged;
        _ui.OnPostDrawUIRoot += DrawCaptureIndicator;
        _cfg.OnValueChanged(CVars.AudioCaptureDevice, OnCaptureDeviceChanged);
        _cfg.OnValueChanged(CVars.AudioCaptureIndicator, OnCaptureIndicatorChanged, true);
        return _capture;
    }

    /// <summary>
    /// Remembered answers live beside the user data folder rather than in it: content can write every file in there
    /// and every CVar, so it could otherwise answer the prompt for the player.
    /// </summary>
    private string? GetCaptureConsentPath()
    {
        if (_cache.UserData is not WritableDirProvider userData)
            return null;

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(userData.RootDir));
        return parent == null ? null : Path.Combine(parent, CaptureConsentFile);
    }

    private void OnCaptureDeviceChosen(string device)
    {
        // Remembered like the output device.
        _cfg.SetCVar(CVars.AudioCaptureDevice, device);
        _cfg.SaveToFile();
    }

    private void OnCaptureDeviceChanged(string device)
    {
        _capture?.SetDevice(device);
    }

    private void OnCaptureIndicatorChanged(bool show)
    {
        _showCaptureIndicator = show;
    }

    private string GetCaptureServer()
    {
        return _net.ServerChannel?.RemoteEndPoint.ToString() ?? Loc.GetString("audio-capture-consent-local");
    }

    private void OnCaptureConnectionChanged(ClientConnectionState state)
    {
        // Content of the server we left keeps no microphone.
        if (state == ClientConnectionState.NotConnecting)
            _capture?.CloseAll();
    }

    private void ShutdownCapture()
    {
        if (_capture == null)
            return;

        _capture.CloseAll();
        _net.ClientConnectStateChanged -= OnCaptureConnectionChanged;
        _ui.OnPostDrawUIRoot -= DrawCaptureIndicator;
        _cfg.UnsubValueChanged(CVars.AudioCaptureDevice, OnCaptureDeviceChanged);
        _cfg.UnsubValueChanged(CVars.AudioCaptureIndicator, OnCaptureIndicatorChanged);
    }

    private void DrawCaptureIndicator(PostDrawUIRootEventArgs args)
    {
        if (!_showCaptureIndicator || args.Root != _ui.RootControl || _capture is not { AnyCapturing: true })
            return;

        _captureIndicatorFont ??= new VectorFont(
            _cache.GetResource<FontResource>("/EngineFonts/NotoSans/NotoSans-Regular.ttf"), 12);

        var handle = args.DrawingHandle;
        var scale = args.Root.UIScale;
        var text = Loc.GetString("audio-capture-indicator");
        var textSize = handle.GetDimensions(_captureIndicatorFont, text, scale);
        var padding = 6f * scale;
        var dot = 5f * scale;
        var width = textSize.X + dot * 2f + padding * 3f;
        var height = textSize.Y + padding * 2f;
        var left = (args.Root.PixelSize.X - width) / 2f;
        var top = 8f * scale;

        handle.DrawRect(new UIBox2(left, top, left + width, top + height), Color.Black.WithAlpha(0.75f));
        handle.DrawCircle(new Vector2(left + padding + dot, top + height / 2f), dot, Color.Red);
        handle.DrawString(_captureIndicatorFont,
            new Vector2(left + padding * 2f + dot * 2f, top + padding),
            text,
            scale,
            Color.White);
    }
}
