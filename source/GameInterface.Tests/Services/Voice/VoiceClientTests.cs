using Common.Network;
using Common.Voice;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.Voice;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceClientTests
{
    private readonly Mock<IVoiceGameInput> input = new();
    private readonly Mock<IVoiceAudio> audio = new();
    private readonly Mock<INetwork> network = new();
    private readonly Mock<IVoiceClock> clock = new();
    private VoiceInputSnapshot? sampled;

    private VoiceClient Create(bool configured = true)
    {
        clock.SetupGet(x => x.Milliseconds).Returns(1000);
        bool testing = false;
        audio.SetupGet(x => x.IsTestingMicrophone).Returns(() => testing);
        audio.Setup(x => x.TestMicrophone(It.IsAny<bool>())).Callback<bool>(value => testing = value);
        var store = new Mock<ICoopOptionsStore>();
        store.Setup(x => x.LoadOrDefault()).Returns(new CoopOptionsData());
        input.Setup(x => x.Sample(It.IsAny<long>(), It.IsAny<VoiceSettings>())).Returns(() => sampled!);
        var client = new VoiceClient(input.Object, audio.Object, clock.Object, network.Object, store.Object, new VoiceTransitWindow());
        if (configured) client.Configure(new VoiceRanges(), 1000);
        return client;
    }

    private void Sample(string context = "campaign", bool speak = true, bool focused = true, bool typing = false)
        => sampled = new VoiceInputSnapshot(new VoicePosition(context, 0, 0, 0, 0, speak, context != ""), focused, typing, true, 1000);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MicrophoneTestExpiryNotifiesEvenWhenStatusTextDoesNotChange(bool configured)
    {
        using var client = Create(configured);
        Sample();
        audio.SetupGet(x => x.Status).Returns("Ready");
        client.TestMicrophone(true);
        Assert.True(client.IsTestingMicrophone);
        client.Tick();
        int notifications = 0;
        client.StatusChanged += () => notifications++;
        clock.SetupGet(x => x.Milliseconds).Returns(6000);
        client.Tick();
        Assert.False(client.IsTestingMicrophone);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void MicrophoneFailureEndsTestAndTickPublishesMeterChangesOnCallingThread()
    {
        using var client = Create();
        Sample();
        client.TestMicrophone(true);
        client.Tick();
        int thread = 0;
        int notifications = 0;
        client.StatusChanged += () => { thread = System.Environment.CurrentManagedThreadId; notifications++; };
        audio.SetupGet(x => x.InputLevel).Returns(0.5f);
        Assert.Equal(0, notifications);
        client.Tick();
        Assert.Equal(1, notifications);
        Assert.Equal(System.Environment.CurrentManagedThreadId, thread);
        Assert.Equal(0.5f, client.InputLevel);
        audio.SetupGet(x => x.IsTestingMicrophone).Returns(false);
        audio.SetupGet(x => x.InputLevel).Returns(0);
        client.Tick();
        Assert.False(client.IsTestingMicrophone);
        Assert.Equal(0, client.InputLevel);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void MicrophoneStartFailureIsNonfatalAndDoesNotLeaveStopLabelActive()
    {
        using var client = Create();
        audio.Setup(x => x.TestMicrophone(true)).Throws(new System.InvalidOperationException("unplugged"));
        client.TestMicrophone(true);
        Assert.False(client.IsTestingMicrophone);
        Assert.Contains("unplugged", client.Status);
    }

    [Fact]
    public void SettingsUpdateAudioImmediatelyAndRestrictiveEpochIsPublishedOnNextTick()
    {
        using var client = Create();
        Sample(); client.Tick();
        audio.Invocations.Clear();
        var settings = client.Settings;
        settings.Volume = 0.4f;
        settings.Sensitivity = 0.1f;
        settings.Deafened = true;
        client.Apply(settings);
        audio.Verify(x => x.Update(It.Is<VoiceInputSnapshot>(s => !s.Position.CanHear && !s.Position.CanSpeak),
            It.Is<VoiceSettings>(s => s.Volume == 0.4f && s.Sensitivity == 0.1f && s.Deafened)), Times.Once);
        client.Tick();
        network.Verify(x => x.SendAll(It.Is<VoiceContextChanged>(m => m.Position.Epoch == 2 && !m.Position.CanHear)), Times.Once);
        client.Tick();
        network.Verify(x => x.SendAll(It.Is<VoiceContextChanged>(m => m.Position.Epoch == 2)), Times.Once);
    }

    [Fact]
    public void MicrophoneTestCanBeStoppedAndDisabled()
    {
        using var client = Create();
        client.TestMicrophone(true);
        Assert.True(client.IsTestingMicrophone);
        client.TestMicrophone(false);
        Assert.False(client.IsTestingMicrophone);
        client.TestMicrophone(true);
        var disabled = client.Settings;
        disabled.Enabled = false;
        client.Apply(disabled);
        Assert.False(client.IsTestingMicrophone);
    }

    [Fact]
    public void ApplyDisabledImmediatelyFencesSendReceiveAndMicrophoneTestUntilReenabled()
    {
        using var client = Create();
        VoiceInputSnapshot? current = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => current = snapshot);
        Sample(); client.Tick();
        var captured = current;
        client.TestMicrophone(true);
        var disabled = client.Settings;
        disabled.Enabled = false;
        client.Apply(disabled);
        Assert.False(current!.Position.CanSpeak);
        Assert.False(current.Position.CanHear);
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        client.Receive(new VoicePacket { SentAt = 1000 });
        client.TestMicrophone(true);
        audio.Verify(x => x.Receive(It.IsAny<VoicePacket>()), Times.Never);
        audio.Verify(x => x.TestMicrophone(true), Times.Once);
        audio.Verify(x => x.TestMicrophone(false), Times.AtLeastOnce);
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Never);
        Assert.Equal("Disabled", client.Status);
        disabled.Enabled = true;
        client.Apply(disabled);
        client.Tick();
        Assert.True(current.Position.CanSpeak);
        Assert.True(current.Position.CanHear);
        audio.Raise(x => x.Encoded += null, current, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Once);
    }

    [Fact]
    public void SceneChangeFencesQueuedCaptureAndSpectatorCannotTransmit()
    {
        using var client = Create();
        VoiceInputSnapshot? first = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => first ??= snapshot);
        Sample();
        client.Tick();
        Sample("battle:a", speak: false);
        client.Tick();
        audio.Raise(x => x.Encoded += null, first!, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Never);
        network.Verify(x => x.SendAll(It.Is<VoiceContextChanged>(p => p.Position.Context == "battle:a" && !p.Position.CanSpeak)), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void RechecksFocusAndChatAfterEncoding(bool focused, bool typing)
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => captured ??= snapshot);
        Sample(); client.Tick();
        Sample(focused: focused, typing: typing); client.Tick();
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Never);
    }

    [Fact]
    public void HeartbeatsDoNotConsumeAudioSequencesAndDeafenChangesEpoch()
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => captured = snapshot);
        Sample(); client.Tick();
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0 && p.Sequence == 1 && p.StateSequence == 2)), Times.Once);
        client.Apply(new VoiceSettings { Deafened = true }); client.Tick();
        Assert.False(captured!.Position.CanHear);
        Assert.False(captured.Position.CanSpeak);
        Assert.Equal(2, captured.Position.Epoch);
    }

    [Theory]
    [InlineData(true, false, VoiceActivation.PushToTalk)]
    [InlineData(false, true, VoiceActivation.PushToTalk)]
    [InlineData(false, false, VoiceActivation.Disabled)]
    public void ApplyingSuppressionFencesPendingEncodeBeforeNextTick(bool muted, bool deafened, VoiceActivation activation)
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => captured = snapshot);
        Sample(); client.Tick();
        client.Apply(new VoiceSettings { Muted = muted, Deafened = deafened, Activation = activation });
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Never);
    }

    [Fact]
    public void LocalMicrophoneTestCannotSendAlreadyEncodedFrames()
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => captured = snapshot);
        Sample(); client.Tick();
        client.TestMicrophone(true);
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Never);
    }

    [Theory]
    [InlineData(VoiceActivation.PushToTalk)]
    [InlineData(VoiceActivation.VoiceActivity)]
    public void BindingCaptureFencesPendingAudioAndTheClosingFrame(VoiceActivation activation)
    {
        using var client = Create();
        client.Apply(new VoiceSettings { Activation = activation });
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => captured = snapshot);
        Sample(); client.Tick();
        var beforeCapture = captured;
        client.SetKeybindCapture(true);
        audio.Raise(x => x.Encoded += null, beforeCapture!, new byte[] { 1 });
        client.Tick();
        Assert.False(captured!.Position.CanSpeak);
        Assert.True(captured.Position.CanHear);
        client.SetKeybindCapture(false);
        audio.Raise(x => x.Encoded += null, beforeCapture!, new byte[] { 1 });
        client.Tick();
        Assert.False(captured.Position.CanSpeak);
        audio.Raise(x => x.Encoded += null, captured, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Never);
        client.Tick();
        Assert.True(captured.Position.CanSpeak);
        audio.Raise(x => x.Encoded += null, captured, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Once);
    }

    [Fact]
    public void ApplyingKeyRebindFencesOldInputAndSamplesTheNewBinding()
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => captured = snapshot);
        Sample(); client.Tick();
        client.Apply(new VoiceSettings { PushToTalkKey = TaleWorlds.InputSystem.InputKey.F12 });
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.Is<VoicePacket>(p => p.Audio.Length > 0)), Times.Never);
        client.Tick();
        input.Verify(x => x.Sample(It.IsAny<long>(), It.Is<VoiceSettings>(s => s.PushToTalkKey == TaleWorlds.InputSystem.InputKey.F12)), Times.Once);
    }

    [Fact]
    public void WaitingForConfigurationDoesNotOpenMicrophoneAndDisposeIsIdempotent()
    {
        var store = new Mock<ICoopOptionsStore>();
        store.Setup(x => x.LoadOrDefault()).Returns(new CoopOptionsData());
        input.Setup(x => x.Sample(It.IsAny<long>(), It.IsAny<VoiceSettings>())).Returns(() => sampled!);
        using var client = new VoiceClient(input.Object, audio.Object, clock.Object, network.Object, store.Object, new VoiceTransitWindow());
        Sample(); client.Tick();
        audio.Verify(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()), Times.Never);
        client.Dispose(); client.Dispose();
        audio.Verify(x => x.Dispose(), Times.Once);
    }
}
