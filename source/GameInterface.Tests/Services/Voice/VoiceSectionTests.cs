using Common.Network;
using Common.Voice;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.VoiceTab.Sections;
using GameInterface.Services.Voice;
using GameInterface.Tests.Services.UI;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Xunit;
using static GameInterface.Tests.Services.Voice.VoiceAudioTests;

namespace GameInterface.Tests.Services.Voice;

[Collection("Voice keybinding UI")]
public class VoiceSectionTests
{
    private static XElement RetryButton(XContainer layout)
    {
        var button = Assert.Single(layout.Descendants("ButtonWidget"),
            x => (string?)x.Attribute("Command.Click") == nameof(VoiceSection.ExecuteRetry));
        Assert.DoesNotContain(button.AncestorsAndSelf().Attributes("IsVisible"), x => x.Value != "@IsSelected");
        Assert.DoesNotContain(button.AncestorsAndSelf().Attributes("IsEnabled"), x => x.Value != "@Enabled");
        Assert.Contains(button.Descendants("TextWidget"), x => (string?)x.Attribute("Text") == "@RetryText");
        return button;
    }

    [Fact]
    public void SourceAndGeneratedVoiceLayoutsExposeRetryCommandAndLabel()
    {
        string moviePath = PopupUIMovieBindingTests.FindMoviePath("CoopOptionsUIMovie.xml");
        string root = Path.GetDirectoryName(Path.GetDirectoryName(moviePath))!;
        var source = XDocument.Load(Path.Combine(root,
            "source/GameInterface/Services/UI/CoopOptions/Providers/VoiceTab/Sections/VoiceSection.xml"));
        var movie = XDocument.Load(moviePath);
        Assert.True(XNode.DeepEquals(RetryButton(source), RetryButton(movie)));
        Assert.NotNull(typeof(VoiceSection).GetProperty("RetryText"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetryButtonRefreshesMicrophonesAndRestoresCaptureWithoutInterruptingReception(bool latchClientError)
    {
        using var h = new WorkerHarness();
        h.Input.Setup(x => x.Microphones()).Returns(new[] { "System default", "Disconnected microphone" });
        var input = new Mock<IVoiceGameInput>();
        bool inputFailed = false;
        input.Setup(x => x.Sample(It.IsAny<long>(), It.IsAny<VoiceSettings>()))
            .Returns<long, VoiceSettings>((epoch, _) => inputFailed
                ? throw new InvalidOperationException("input unavailable")
                : new VoiceInputSnapshot(new VoicePosition("campaign", epoch, 0, 0, 0, true, true),
                    true, false, true, Interlocked.Read(ref h.Now)));
        var clock = new Mock<IVoiceClock>();
        clock.SetupGet(x => x.Milliseconds).Returns(() => Interlocked.Read(ref h.Now));
        var network = new Mock<INetwork>();
        var store = new Mock<ICoopOptionsStore>();
        store.Setup(x => x.LoadOrDefault()).Returns(new CoopOptionsData());
        using var client = new VoiceClient(input.Object, h.Audio, clock.Object, network.Object, store.Object, new VoiceTransitWindow());
        client.Configure(new VoiceRanges(), 1000);
        h.Start();
        client.Tick();
        var section = new VoiceSection(client, client.Settings, store.Object);
        try
        {
            Assert.Equal("Disconnected microphone", section.MicrophoneSelector.ItemList[1].StringItem);
            h.Callbacks.Single().error(new InvalidOperationException("microphone unplugged"));
            Wait(() => Volatile.Read(ref h.InputDisposals) == 1);
            Assert.Contains("Select Retry", client.Status);
            if (latchClientError)
            {
                inputFailed = true;
                client.Tick();
                inputFailed = false;
                input.Invocations.Clear();
                client.Tick();
                input.Verify(x => x.Sample(It.IsAny<long>(), It.IsAny<VoiceSettings>()), Times.Never);
                Assert.Contains("input unavailable", client.Status);
            }
            h.Frame(1);
            h.Frame(2);
            Wait(() => Volatile.Read(ref h.ReceivedCount) == 2);
            h.Advance(1060);
            Wait(() => h.Played.Count == 1);
            h.Input.Setup(x => x.Microphones()).Returns(new[] { "System default", "Reconnected microphone", "USB microphone" });
            var movie = XDocument.Load(PopupUIMovieBindingTests.FindMoviePath("CoopOptionsUIMovie.xml"));
            var button = RetryButton(movie);
            Assert.Equal("Retry / Refresh", section.RetryText);
            section.ExecuteCommand((string)button.Attribute("Command.Click")!, Array.Empty<object>());
            Wait(() => h.Callbacks.Count == 2 && client.Status == "Ready");
            client.Tick();
            Assert.Empty(section.StatusText);
            Assert.Equal(3, section.MicrophoneSelector.ItemList.Count);
            Assert.Equal("Reconnected microphone", section.MicrophoneSelector.ItemList[1].StringItem);
            Assert.Equal("USB microphone", section.MicrophoneSelector.ItemList[2].StringItem);
            Assert.Equal(0, section.MicrophoneSelector.SelectedIndex);
            h.Input.Verify(x => x.Microphones(), Times.Exactly(2));
            if (latchClientError) input.Verify(x => x.Sample(It.IsAny<long>(), It.IsAny<VoiceSettings>()), Times.Once);
            h.Advance(1080);
            client.Tick();
            Wait(() => h.Played.Count == 2);
            h.Callbacks.Last().data(Pcm(8000), 1920);
            Wait(() => h.Encoded.Count == 1);
            Wait(() => network.Invocations.Any(x => x.Arguments.Any(arg => arg is VoicePacket packet && packet.Audio.Length > 0)));
            Assert.Equal(1, h.DecoderCount);
            Assert.Equal(1, Volatile.Read(ref h.Clears));
            h.Device.Verify(x => x.Dispose(), Times.Never);
            h.Output.Verify(x => x.Open(It.IsAny<Action<Exception>>()), Times.Once);
            store.Verify(x => x.Save(It.IsAny<CoopOptionsData>()), Times.Never);
        }
        finally { section.OnFinalize(); }
    }
}
