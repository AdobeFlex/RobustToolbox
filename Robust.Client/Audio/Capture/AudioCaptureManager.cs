using System;
using System.Collections.Generic;

namespace Robust.Client.Audio.Capture;

/// <summary>
/// Hands out microphone captures and keeps the player's per-server answers to the consent prompt.
/// No device is opened before the player allows the server and picks the microphone.
/// </summary>
internal sealed class AudioCaptureManager
{
    private readonly IAudioCaptureBackend _backend;
    private readonly IAudioCapturePrompt _prompt;
    private readonly AudioCaptureConsentStore _store;

    private readonly List<AudioCapture> _captures = new();

    /// <summary>
    /// Answers given during this game session, by server.
    /// </summary>
    private readonly Dictionary<string, bool> _decisions = new();

    private bool _prompting;

    /// <summary>
    /// The microphone the player picked, or empty if they never did.
    /// </summary>
    public string Device { get; private set; }

    /// <summary>
    /// Raised when the player picks a different microphone in the prompt, so it can be saved.
    /// </summary>
    public event Action<string>? DeviceChosen;

    public AudioCaptureManager(
        IAudioCaptureBackend backend,
        IAudioCapturePrompt prompt,
        AudioCaptureConsentStore store,
        string device)
    {
        _backend = backend;
        _prompt = prompt;
        _store = store;
        Device = device;
    }

    public bool AnyCapturing
    {
        get
        {
            foreach (var capture in _captures)
            {
                if (capture.Capturing)
                    return true;
            }

            return false;
        }
    }

    public IReadOnlyList<string> GetDevices()
    {
        return _backend.GetDevices();
    }

    public IAudioCapture Request(string server, int sampleRate)
    {
        var capture = new AudioCapture(this, _backend, sampleRate);
        _captures.Add(capture);

        bool? allowed = _decisions.TryGetValue(server, out var answer) || _store.TryGet(server, out answer)
            ? answer
            : null;

        switch (allowed)
        {
            case false:
                capture.Deny();
                break;
            // An allowed server still asks again when the picked microphone is gone, rather than recording another.
            case true when IsPresent(Device):
                capture.Open(Device);
                break;
            default:
                Prompt(server);
                break;
        }

        return capture;
    }

    /// <summary>
    /// Forgets the server's answer, remembered or not, and closes every capture so the next request asks again.
    /// </summary>
    public void Reset(string server)
    {
        _decisions.Remove(server);
        _store.Forget(server);
        CloseAll();
    }

    /// <summary>
    /// Moves open captures to another microphone.
    /// </summary>
    public void SetDevice(string device)
    {
        if (device == Device)
            return;

        Device = device;

        // StateChanged handlers may request or dispose captures.
        foreach (var capture in _captures.ToArray())
        {
            capture.Reopen(device);
        }
    }

    /// <summary>
    /// Closes every capture and the prompt, as when leaving a server.
    /// </summary>
    public void CloseAll()
    {
        foreach (var capture in _captures.ToArray())
        {
            capture.Close();
        }

        _captures.Clear();

        // After the captures, so pending ones end up closed rather than denied.
        if (_prompting)
            _prompt.Close();
    }

    internal void Remove(AudioCapture capture)
    {
        _captures.Remove(capture);
    }

    private bool IsPresent(string device)
    {
        if (device.Length == 0)
            return false;

        foreach (var present in _backend.GetDevices())
        {
            if (present == device)
                return true;
        }

        return false;
    }

    private void Prompt(string server)
    {
        if (_prompting)
            return;

        _prompting = true;
        _prompt.Show(server,
            _backend.GetDevices(),
            Device,
            (device, remember) => OnAnswered(server, device, remember),
            OnPromptClosed);
    }

    private void OnAnswered(string server, string? device, bool remember)
    {
        _decisions[server] = device != null;
        if (remember)
            _store.Remember(server, device != null);

        if (device != null && device != Device)
        {
            Device = device;
            DeviceChosen?.Invoke(device);
        }

        ResolvePending(device);
    }

    private void OnPromptClosed()
    {
        // Closing without an answer refuses the waiting requests, but the next one asks again.
        _prompting = false;
        ResolvePending(null);
    }

    private void ResolvePending(string? device)
    {
        // Opening a device raises StateChanged, whose handlers may request or dispose captures.
        foreach (var capture in _captures.ToArray())
        {
            if (device != null)
                capture.Open(device);
            else
                capture.Deny();
        }
    }
}
