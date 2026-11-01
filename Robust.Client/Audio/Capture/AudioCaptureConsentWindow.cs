using System;
using System.Collections.Generic;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Robust.Client.Audio.Capture;

/// <summary>
/// Engine-owned prompt asking whether the current server may use the microphone, and which one.
/// Content cannot answer it, so a server can never record without the player's say-so.
/// </summary>
internal sealed class AudioCaptureConsentWindow : DefaultWindow
{
    private const string OpenAlSoftPrefix = "OpenAL Soft on ";

    /// <summary>
    /// Id of the "pick a microphone" entry; devices follow from 1.
    /// </summary>
    private const int NoDeviceId = 0;

    private readonly OptionButton _devices = new();
    private readonly Button _allow = new();
    private readonly CheckBox _remember = new();

    /// <summary>
    /// Raised with the microphone the player allowed, or null if they refused, and whether to keep that answer
    /// across restarts. Closing the window without answering raises nothing.
    /// </summary>
    public event Action<string?, bool>? OnAnswer;

    /// <param name="server">Shown so the player knows who is asking.</param>
    /// <param name="devices">Microphones to pick from.</param>
    /// <param name="selected">The microphone picked last time, preselected if it is still present.</param>
    public AudioCaptureConsentWindow(string server, IReadOnlyList<string> devices, string selected)
    {
        Title = Loc.GetString("audio-capture-consent-title");
        Resizable = false;

        // Nothing is preselected on first use: the system default is often a silent virtual device.
        _devices.AddItem(Loc.GetString(devices.Count > 0
                ? "audio-capture-consent-device-none"
                : "audio-capture-consent-device-missing"),
            NoDeviceId);

        for (var i = 0; i < devices.Count; i++)
        {
            var id = i + 1;
            _devices.AddItem(FormatDevice(devices[i]), id);
            _devices.SetItemMetadata(_devices.GetIdx(id), devices[i]);

            if (devices[i] == selected)
                _devices.SelectId(id);
        }

        _devices.HorizontalExpand = true;
        _devices.OnItemSelected += args =>
        {
            _devices.SelectId(args.Id);
            UpdateAllow();
        };

        _allow.Text = Loc.GetString("audio-capture-consent-allow");
        _allow.OnPressed += _ =>
        {
            if (_devices.SelectedMetadata is string device)
                Answer(device);
        };

        var deny = new Button { Text = Loc.GetString("audio-capture-consent-deny") };
        deny.OnPressed += _ => Answer(null);

        _remember.Text = Loc.GetString("audio-capture-consent-remember");

        // RichTextLabel wraps within MaxWidth; Label would not.
        var explanation = new RichTextLabel();
        explanation.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString("audio-capture-consent-text")));

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            MaxWidth = 420,
            Children =
            {
                explanation,
                new Label { Text = Loc.GetString("audio-capture-consent-server", ("server", server)) },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children =
                    {
                        new Label { Text = Loc.GetString("audio-capture-consent-device") },
                        _devices,
                    },
                },
                _remember,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children = { _allow, deny },
                },
            },
        });

        UpdateAllow();
    }

    private void UpdateAllow()
    {
        _allow.Disabled = _devices.SelectedId == NoDeviceId;
    }

    private void Answer(string? device)
    {
        OnAnswer?.Invoke(device, _remember.Pressed);
        Close();
    }

    private static string FormatDevice(string device)
    {
        if (!device.StartsWith(OpenAlSoftPrefix, StringComparison.Ordinal))
            return device;

        return IAudioManager.ConvertAudioDeviceNameForDisplay(device[OpenAlSoftPrefix.Length..]);
    }
}
