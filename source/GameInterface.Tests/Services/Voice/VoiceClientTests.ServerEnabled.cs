using Common.PacketHandlers;
using Common.Voice;
using GameInterface.Services.Voice;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceClientTests
{
    [Fact]
    public void ServerDisableFencesCaptureAndReceiveWithoutChangingLocalPreferences()
    {
        using var client = Create(); Sample(); client.Tick();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((snapshot, _) => captured = snapshot);
        client.Tick();
        client.SetServerEnabled(false);
        audio.Verify(x => x.Update(It.Is<VoiceInputSnapshot>(s => !s.Position.CanHear && !s.Position.CanSpeak),
            It.Is<VoiceSettings>(s => !s.Enabled)), Times.Once);
        Assert.True(client.Settings.Enabled);
        Assert.Equal("Disabled by server", client.Status);
        network.Invocations.Clear();
        audio.Raise(x => x.Encoded += null, captured, new byte[] { 1 });
        network.Verify(x => x.SendAll(It.IsAny<IPacket>()), Times.Never);
        client.Receive(new VoicePacket { SentAt = 1000 });
        audio.Verify(x => x.Receive(It.IsAny<VoicePacket>()), Times.Never);
        client.SetServerEnabled(true);
        client.Tick();
        audio.Verify(x => x.Update(It.Is<VoiceInputSnapshot>(s => s.Position.CanHear && s.Position.CanSpeak),
            It.Is<VoiceSettings>(s => s.Enabled)), Times.AtLeastOnce);
    }

    [Fact]
    public void LocalHideOnlyHidesPresentationAndServerDisableAlwaysHidesIt()
    {
        using var client = Create();
        audio.SetupGet(x => x.AudibleSpeakers).Returns(new[] { "platform:1" });
        Assert.Single(client.AudibleSpeakers);
        var options = client.Settings; options.ShowTalkingPlayers = false;
        client.Apply(options);
        Assert.Empty(client.AudibleSpeakers);
        Assert.True(client.Settings.Enabled);
        options.ShowTalkingPlayers = true; client.Apply(options);
        Assert.Single(client.AudibleSpeakers);
        client.SetServerEnabled(false);
        Assert.Empty(client.AudibleSpeakers);
    }
}
