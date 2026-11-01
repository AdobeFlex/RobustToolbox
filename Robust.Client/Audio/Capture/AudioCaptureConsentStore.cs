using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Robust.Shared.Log;

namespace Robust.Client.Audio.Capture;

/// <summary>
/// Microphone answers the player asked not to be asked again about, kept across restarts.
/// The file lives next to the user data folder rather than in it: content can write every file in there and every
/// CVar, so it could otherwise grant itself the microphone.
/// </summary>
internal sealed class AudioCaptureConsentStore
{
    private readonly Dictionary<string, bool> _answers = new();
    private readonly string? _path;
    private readonly ISawmill _sawmill;

    /// <param name="path">File to keep the answers in, or null to keep them in memory only.</param>
    public AudioCaptureConsentStore(string? path, ISawmill sawmill)
    {
        _path = path;
        _sawmill = sawmill;
        Load();
    }

    public bool TryGet(string server, out bool allowed)
    {
        return _answers.TryGetValue(server, out allowed);
    }

    public void Remember(string server, bool allowed)
    {
        _answers[server] = allowed;
        Save();
    }

    public void Forget(string server)
    {
        if (_answers.Remove(server))
            Save();
    }

    private void Load()
    {
        if (_path == null || !File.Exists(_path))
            return;

        try
        {
            var answers = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(_path));
            if (answers == null)
                return;

            foreach (var (server, allowed) in answers)
            {
                _answers[server] = allowed;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Everyone gets asked again, which is the safe way to lose this file.
            _sawmill.Warning($"Could not read remembered microphone answers from {_path}: {e.Message}");
        }
    }

    private void Save()
    {
        if (_path == null)
            return;

        try
        {
            if (_answers.Count == 0)
            {
                File.Delete(_path);
                return;
            }

            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_answers));
            File.Move(temp, _path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _sawmill.Warning($"Could not save remembered microphone answers to {_path}: {e.Message}");
        }
    }
}
