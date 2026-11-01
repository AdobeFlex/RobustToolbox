using System;
using System.Collections.Generic;

namespace Robust.Client.Audio.Capture;

/// <summary>
/// Asks the player whether a server may use the microphone, and which one.
/// </summary>
internal interface IAudioCapturePrompt
{
    /// <param name="server">The server asking.</param>
    /// <param name="devices">Microphones to pick from.</param>
    /// <param name="selected">The microphone picked last time, or empty.</param>
    /// <param name="answered">
    /// Raised with the allowed microphone, or null for a refusal, and whether to keep the answer across restarts.
    /// </param>
    /// <param name="closed">Raised whenever the prompt goes away, answered or not.</param>
    void Show(
        string server,
        IReadOnlyList<string> devices,
        string selected,
        Action<string?, bool> answered,
        Action closed);

    void Close();
}

/// <summary>
/// Shows <see cref="AudioCaptureConsentWindow"/>, which content cannot answer.
/// </summary>
internal sealed class AudioCaptureConsentPrompt : IAudioCapturePrompt
{
    private AudioCaptureConsentWindow? _window;

    public void Show(
        string server,
        IReadOnlyList<string> devices,
        string selected,
        Action<string?, bool> answered,
        Action closed)
    {
        var window = new AudioCaptureConsentWindow(server, devices, selected);
        _window = window;
        window.OnAnswer += answered;
        window.OnClose += () =>
        {
            _window = null;
            closed();
        };

        window.OpenCentered();
    }

    public void Close()
    {
        _window?.Close();
    }
}
