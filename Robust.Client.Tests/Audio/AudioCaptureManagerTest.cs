using NUnit.Framework;
using Robust.Client.Audio.Capture;
using Robust.Shared.Log;

namespace Robust.Client.Tests.Audio;

[TestFixture, Parallelizable]
[TestOf(typeof(AudioCaptureManager))]
public sealed class AudioCaptureManagerTest
{
    private const string Server = "10.0.0.1:1212";
    private const string OtherServer = "10.0.0.2:1212";
    private const string Microphone = "Headset";
    private const string OtherMicrophone = "Webcam";
    private const int SampleRate = 48000;

    private string _consentPath = default!;
    private FakeBackend _backend = default!;
    private FakePrompt _prompt = default!;

    [SetUp]
    public void SetUp()
    {
        _consentPath = Path.Combine(Path.GetTempPath(), $"robust-capture-consent-{Guid.NewGuid():N}.json");
        _backend = new FakeBackend(Microphone, OtherMicrophone);
        _prompt = new FakePrompt();
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(_consentPath);
    }

    private AudioCaptureManager CreateManager(string device = "")
    {
        var store = new AudioCaptureConsentStore(_consentPath, new LogManager().GetSawmill("audio.capture"));
        return new AudioCaptureManager(_backend, _prompt, store, device);
    }

    [Test]
    public void RequestWaitsForThePlayer()
    {
        var capture = CreateManager().Request(Server, SampleRate);

        Assert.Multiple(() =>
        {
            Assert.That(capture.State, Is.EqualTo(AudioCaptureState.Pending));
            Assert.That(_prompt.Shown, Is.EqualTo(1));
            Assert.That(_backend.Opened, Is.Empty);
        });
    }

    [Test]
    public void AllowOpensThePickedMicrophone()
    {
        var manager = CreateManager();
        string? chosen = null;
        manager.DeviceChosen += device => chosen = device;

        var capture = manager.Request(Server, SampleRate);
        _prompt.Answer(OtherMicrophone, false);

        Assert.Multiple(() =>
        {
            Assert.That(capture.State, Is.EqualTo(AudioCaptureState.Open));
            Assert.That(_backend.Opened.Select(d => d.Name), Is.EqualTo(new[] { OtherMicrophone }));
            Assert.That(chosen, Is.EqualTo(OtherMicrophone));
            Assert.That(manager.Device, Is.EqualTo(OtherMicrophone));
        });
    }

    [Test]
    public void AnswerCoversEveryWaitingRequest()
    {
        var manager = CreateManager();

        var first = manager.Request(Server, SampleRate);
        var second = manager.Request(Server, SampleRate);
        _prompt.Answer(Microphone, false);

        Assert.Multiple(() =>
        {
            Assert.That(_prompt.Shown, Is.EqualTo(1));
            Assert.That(first.State, Is.EqualTo(AudioCaptureState.Open));
            Assert.That(second.State, Is.EqualTo(AudioCaptureState.Open));
        });
    }

    [Test]
    public void DenyIsKeptForTheSession()
    {
        var manager = CreateManager();

        var first = manager.Request(Server, SampleRate);
        _prompt.Answer(null, false);
        var second = manager.Request(Server, SampleRate);

        Assert.Multiple(() =>
        {
            Assert.That(first.State, Is.EqualTo(AudioCaptureState.Denied));
            Assert.That(second.State, Is.EqualTo(AudioCaptureState.Denied));
            Assert.That(_prompt.Shown, Is.EqualTo(1));
            Assert.That(_backend.Opened, Is.Empty);
        });
    }

    [Test]
    public void AnswerIsPerServer()
    {
        var manager = CreateManager();

        manager.Request(Server, SampleRate);
        _prompt.Answer(Microphone, false);
        var other = manager.Request(OtherServer, SampleRate);

        Assert.Multiple(() =>
        {
            Assert.That(other.State, Is.EqualTo(AudioCaptureState.Pending));
            Assert.That(_prompt.Shown, Is.EqualTo(2));
            Assert.That(_prompt.LastServer, Is.EqualTo(OtherServer));
        });
    }

    [Test]
    public void ClosingThePromptRefusesOnlyThisRequest()
    {
        var manager = CreateManager();

        var first = manager.Request(Server, SampleRate);
        _prompt.Close();
        var second = manager.Request(Server, SampleRate);

        Assert.Multiple(() =>
        {
            Assert.That(first.State, Is.EqualTo(AudioCaptureState.Denied));
            Assert.That(second.State, Is.EqualTo(AudioCaptureState.Pending));
            Assert.That(_prompt.Shown, Is.EqualTo(2));
        });
    }

    [Test]
    public void AllowedServerAsksAgainWhenTheMicrophoneIsGone()
    {
        var manager = CreateManager();

        manager.Request(Server, SampleRate).Dispose();
        _prompt.Answer(Microphone, false);
        _backend.Devices.Remove(Microphone);
        var capture = manager.Request(Server, SampleRate);

        Assert.Multiple(() =>
        {
            Assert.That(capture.State, Is.EqualTo(AudioCaptureState.Pending));
            Assert.That(_prompt.Shown, Is.EqualTo(2));
        });
    }

    [Test]
    public void FailedDeviceIsReported()
    {
        _backend.Broken.Add(Microphone);

        var capture = CreateManager().Request(Server, SampleRate);
        _prompt.Answer(Microphone, false);

        Assert.That(capture.State, Is.EqualTo(AudioCaptureState.Failed));
    }

    [Test]
    public void SessionAnswersAreForgottenOnRestart()
    {
        CreateManager().Request(Server, SampleRate);
        _prompt.Answer(Microphone, false);

        var capture = CreateManager(Microphone).Request(Server, SampleRate);

        Assert.That(capture.State, Is.EqualTo(AudioCaptureState.Pending));
    }

    [Test]
    public void RememberedAnswersSurviveRestart()
    {
        var first = CreateManager();
        first.Request(Server, SampleRate);
        _prompt.Answer(Microphone, true);
        first.Request(OtherServer, SampleRate);
        _prompt.Answer(null, true);

        var manager = CreateManager(Microphone);
        var allowed = manager.Request(Server, SampleRate);
        var denied = manager.Request(OtherServer, SampleRate);

        Assert.Multiple(() =>
        {
            Assert.That(allowed.State, Is.EqualTo(AudioCaptureState.Open));
            Assert.That(denied.State, Is.EqualTo(AudioCaptureState.Denied));
            Assert.That(_prompt.Shown, Is.EqualTo(2));
        });
    }

    [Test]
    public void ResetForgetsOnlyTheCurrentServer()
    {
        var manager = CreateManager();
        manager.Request(OtherServer, SampleRate);
        _prompt.Answer(Microphone, true);
        var capture = manager.Request(Server, SampleRate);
        _prompt.Answer(Microphone, true);
        capture.Start();

        manager.Reset(Server);

        Assert.Multiple(() =>
        {
            Assert.That(capture.State, Is.EqualTo(AudioCaptureState.Closed));
            Assert.That(capture.Capturing, Is.False);
            Assert.That(_backend.Opened.All(d => d.Disposed), Is.True);
            Assert.That(manager.Request(Server, SampleRate).State, Is.EqualTo(AudioCaptureState.Pending));
        });

        _prompt.Close();
        var restarted = CreateManager(Microphone);
        Assert.Multiple(() =>
        {
            Assert.That(restarted.Request(Server, SampleRate).State, Is.EqualTo(AudioCaptureState.Pending));
            Assert.That(restarted.Request(OtherServer, SampleRate).State, Is.EqualTo(AudioCaptureState.Open));
        });
    }

    [Test]
    public void DeviceChangeMovesOpenCaptures()
    {
        var manager = CreateManager();
        var capture = manager.Request(Server, SampleRate);
        _prompt.Answer(Microphone, false);
        capture.Start();

        manager.SetDevice(OtherMicrophone);

        Assert.Multiple(() =>
        {
            Assert.That(_backend.Opened.Select(d => d.Name), Is.EqualTo(new[] { Microphone, OtherMicrophone }));
            Assert.That(_backend.Opened[0].Disposed, Is.True);
            Assert.That(_backend.Opened[1].Started, Is.True);
            Assert.That(capture.Capturing, Is.True);
        });
    }

    [Test]
    public void CloseAllClosesWaitingRequestsWithoutDenying()
    {
        var manager = CreateManager();
        var capture = manager.Request(Server, SampleRate);
        var states = new List<AudioCaptureState>();
        capture.StateChanged += states.Add;

        manager.CloseAll();

        Assert.Multiple(() =>
        {
            Assert.That(states, Is.EqualTo(new[] { AudioCaptureState.Closed }));
            Assert.That(_prompt.Open, Is.False);
        });
    }

    [Test]
    public void ReadReturnsRecordedSamples()
    {
        var capture = CreateManager().Request(Server, SampleRate);
        _prompt.Answer(Microphone, false);
        capture.Start();
        _backend.Opened[0].Samples = 300;

        var buffer = new short[200];

        Assert.Multiple(() =>
        {
            Assert.That(capture.Read(buffer), Is.EqualTo(200));
            Assert.That(capture.Read(buffer), Is.EqualTo(100));
            Assert.That(capture.Read(buffer), Is.EqualTo(0));
        });
    }

    private sealed class FakeBackend : IAudioCaptureBackend
    {
        public readonly List<string> Devices;
        public readonly HashSet<string> Broken = new();
        public readonly List<FakeDevice> Opened = new();

        public FakeBackend(params string[] devices)
        {
            Devices = new List<string>(devices);
        }

        public IReadOnlyList<string> GetDevices()
        {
            return Devices.ToArray();
        }

        public ICaptureDevice? Open(string device, int sampleRate)
        {
            if (!Devices.Contains(device) || Broken.Contains(device))
                return null;

            var opened = new FakeDevice(device);
            Opened.Add(opened);
            return opened;
        }
    }

    private sealed class FakeDevice : ICaptureDevice
    {
        public readonly string Name;
        public bool Started;
        public bool Disposed;
        public int Samples;

        public FakeDevice(string name)
        {
            Name = name;
        }

        public int AvailableSamples => Samples;

        public void Start()
        {
            Started = true;
        }

        public void Stop()
        {
            Started = false;
        }

        public void Read(Span<short> buffer)
        {
            Assert.That(buffer.Length, Is.LessThanOrEqualTo(Samples));
            buffer.Fill(1);
            Samples -= buffer.Length;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    /// <summary>
    /// Behaves like the engine window: answering raises the answer and then closes it.
    /// </summary>
    private sealed class FakePrompt : IAudioCapturePrompt
    {
        public int Shown;
        public string? LastServer;
        public bool Open;

        private Action<string?, bool>? _answered;
        private Action? _closed;

        public void Show(
            string server,
            IReadOnlyList<string> devices,
            string selected,
            Action<string?, bool> answered,
            Action closed)
        {
            Assert.That(Open, Is.False, "The prompt was shown twice at once.");
            Shown++;
            LastServer = server;
            Open = true;
            _answered = answered;
            _closed = closed;
        }

        public void Answer(string? device, bool remember)
        {
            Assert.That(Open, Is.True, "No prompt to answer.");
            _answered!(device, remember);
            Close();
        }

        public void Close()
        {
            if (!Open)
                return;

            Open = false;
            _closed!();
        }
    }
}
