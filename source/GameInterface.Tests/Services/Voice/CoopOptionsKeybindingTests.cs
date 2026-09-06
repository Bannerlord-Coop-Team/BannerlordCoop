using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.VoiceTab;
using GameInterface.Services.UI.CoopOptions.Providers.VoiceTab.Sections;
using GameInterface.Services.Voice;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GameInterface.Tests.Services.UI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

[Collection("Voice keybinding UI")]
public class CoopOptionsKeybindingTests
{
    [Fact]
    public void VoiceLayoutUsesNativeSelectorsTogglesAndSlidersWithGroupedSections()
    {
        var movie = XDocument.Load(PopupUIMovieBindingTests.FindMoviePath("CoopOptionsUIMovie.xml"));
        var voice = Assert.Single(movie.Descendants("ListPanel").Where(x => (string?)x.Attribute("DataSource") == "{VoiceTab}"));
        Assert.Equal(2, voice.Descendants("Standard.DropdownWithHorizontalControl").Count());
        Assert.Equal(2, voice.Descendants("Standard.Slider.Float").Count());
        foreach (string setting in new[] { "@Enabled", "@Muted", "@Deafened", "@ShowTalkingPlayers" })
            Assert.Contains(voice.Descendants("ButtonWidget"), x => (string?)x.Attribute("IsSelected") == setting);
        foreach (string title in new[] { "@InputText", "@PlaybackText", "@MicrophoneTestText" })
            Assert.Contains(voice.Descendants("TextWidget"), x => (string?)x.Attribute("Text") == title);
        var mute = Assert.Single(voice.Descendants("ButtonWidget").Where(x => (string?)x.Attribute("IsSelected") == "@Muted"));
        var deafen = Assert.Single(voice.Descendants("ButtonWidget").Where(x => (string?)x.Attribute("IsSelected") == "@Deafened"));
        Assert.Same(deafen.Parent!.Parent, mute.Parent!.Parent!.ElementsAfterSelf().First());
    }

    [Fact]
    public void GeneratedVoiceMovieUsesNativeKeyRowAndRevertCommands()
    {
        var movie = XDocument.Load(PopupUIMovieBindingTests.FindMoviePath("CoopOptionsUIMovie.xml"));
        var row = Assert.Single(movie.Descendants("OptionsKeyItemListPanel").Where(x => (string?)x.Attribute("DataSource") == "{PushToTalkKey}"));
        Assert.Contains(row.Descendants("ButtonWidget"), button => (string?)button.Attribute("Command.Click") == "ExecuteKeybindRequest");
        Assert.Contains(row.Descendants("ButtonWidget"), button => (string?)button.Attribute("Command.Click") == "ExecuteRevert");
        Assert.Contains(movie.Descendants("ButtonWidget"), button => (string?)button.Attribute("Command.Click") == "ExecuteResetKey");
        Assert.Equal("@IsKeybindingEnabled", (string?)row.Parent?.Parent?.Attribute("IsEnabled"));
    }

    [Theory]
    [InlineData("CoopOptionsUI.cs", "new CoopOptionsGauntletLayer(this, _dataSource)")]
    [InlineData("CoopOptionsOverlay.cs", "new CoopOptionsGauntletLayer(owner, dataSource)")]
    public void BothOptionsEntryPointsUseAndCloseTheSharedPopupLayer(string file, string construction)
    {
        string root = Path.GetDirectoryName(Path.GetDirectoryName(PopupUIMovieBindingTests.FindMoviePath("CoopOptionsUIMovie.xml")))!;
        string source = File.ReadAllText(Path.Combine(root, "source/GameInterface/Services/UI/CoopOptions", file));
        Assert.Contains(construction, source);
        Assert.Contains(".CloseKeybinding();", source);
    }

    private sealed class Popup : ICoopKeybindingPopup
    {
        public bool IsActive { get; private set; }
        public int Ticks;
        public void Toggle(bool active) => IsActive = active;
        public void Tick() => Ticks++;
    }

    [Theory]
    [InlineData(InputKey.F12, InputKey.F12)]
    [InlineData(InputKey.Escape, InputKey.Q)]
    [InlineData(InputKey.ControllerLRight, InputKey.Q)]
    [InlineData(InputKey.LeftMouseButton, InputKey.LeftMouseButton)]
    public void NativeKeyCommandOpensPopupAndOnlyValidKeyboardInputImmediatelySavesAKey(InputKey captured, InputKey expected)
    {
        var voice = new Mock<IVoiceClient>();
        voice.Setup(x => x.Microphones()).Returns(new[] { "System default" });
        var popup = new Popup();
        Action<Key>? done = null;
        var factory = new Mock<ICoopKeybindingPopupFactory>();
        factory.Setup(x => x.Create(It.IsAny<Action<Key>>(), It.IsAny<ScreenBase>()))
            .Callback<Action<Key>, ScreenBase>((callback, _) => done = callback).Returns(popup);
        var window = new Mock<IVoiceWindowFocus>();
        window.SetupGet(x => x.IsFocused).Returns(true);
        using var broker = new MessageBroker();
        var store = new Mock<ICoopOptionsStore>();
        store.Setup(x => x.LoadOrDefault()).Returns(new CoopOptionsData());
        var options = new CoopOptionsVM(store.Object, broker, new ICoopOptionsTabProvider[] { new VoiceOptionsTabProvider(voice.Object, store.Object) }, default, () => { });
        using var binding = new CoopOptionsKeybinding(factory.Object, voice.Object, window.Object);
        int focused = 0;
        binding.Attach(null!, options, () => focused++);
        var section = Assert.IsType<VoiceSection>(Assert.Single(options.VoiceTab.Sections));
        section.PushToTalkKey.ExecuteCommand("ExecuteKeybindRequest", Array.Empty<object>());
        Assert.True(popup.IsActive);
        voice.Verify(x => x.SetKeybindCapture(true), Times.Once);
        binding.Tick();
        Assert.Equal(1, popup.Ticks);
        done!(new Key(captured));
        Assert.False(popup.IsActive);
        Assert.Equal(expected, section.PushToTalkKey.CurrentKey.InputKey);
        voice.Verify(x => x.SetKeybindCapture(false), Times.Once);
        int changes = expected == InputKey.Q ? 0 : 1;
        voice.Verify(x => x.Apply(It.Is<VoiceSettings>(s => s.PushToTalkKey == expected)), Times.Exactly(changes));
        store.Verify(x => x.Save(It.Is<CoopOptionsData>(data => VoiceOptionsTabProvider.Load(data).PushToTalkKey == expected)), Times.Exactly(changes));
        Assert.Equal(1, focused);
        options.OnFinalize();
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("focus")]
    [InlineData("close")]
    public void CancelFocusLossAndClosureReleaseCaptureWithoutChangingTheBinding(string reason)
    {
        var voice = new Mock<IVoiceClient>();
        voice.Setup(x => x.Microphones()).Returns(new[] { "System default" });
        var popup = new Popup();
        var factory = new Mock<ICoopKeybindingPopupFactory>();
        factory.Setup(x => x.Create(It.IsAny<Action<Key>>(), It.IsAny<ScreenBase>())).Returns(popup);
        bool focused = true;
        var window = new Mock<IVoiceWindowFocus>();
        window.SetupGet(x => x.IsFocused).Returns(() => focused);
        using var broker = new MessageBroker();
        var store = new Mock<ICoopOptionsStore>();
        store.Setup(x => x.LoadOrDefault()).Returns(new CoopOptionsData());
        var options = new CoopOptionsVM(store.Object, broker, new ICoopOptionsTabProvider[] { new VoiceOptionsTabProvider(voice.Object, store.Object) }, default, () => { });
        using var binding = new CoopOptionsKeybinding(factory.Object, voice.Object, window.Object);
        binding.Attach(null!, options, () => { });
        var section = Assert.IsType<VoiceSection>(Assert.Single(options.VoiceTab.Sections));
        section.PushToTalkKey.ExecuteCommand("ExecuteKeybindRequest", Array.Empty<object>());
        if (reason == "cancel") popup.Toggle(false);
        if (reason == "focus") focused = false;
        if (reason == "close") binding.Dispose();
        binding.Tick();
        Assert.False(popup.IsActive);
        Assert.Equal(InputKey.Q, section.PushToTalkKey.CurrentKey.InputKey);
        store.Verify(x => x.Save(It.IsAny<CoopOptionsData>()), Times.Never);
        voice.Verify(x => x.Apply(It.IsAny<VoiceSettings>()), Times.Never);
        voice.Verify(x => x.SetKeybindCapture(false), Times.Once);
        options.OnFinalize();
        options.OnFinalize();
        voice.VerifyRemove(x => x.StatusChanged -= It.IsAny<Action>(), Times.Once);
    }
}
