using Common.Messaging;
using Common.Voice;
using GameInterface.Configuration;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.VoiceTab;
using GameInterface.Services.UI.CoopOptions.Providers.VoiceTab.Sections;
using GameInterface.Services.Voice;
using Moq;
using System;
using System.IO;
using Xunit;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions.GameKeys;

namespace GameInterface.Tests.Services.Voice;

[Collection("Voice keybinding UI")]
public class VoiceOptionsTests
{
    private sealed class Harness : IDisposable
    {
        public readonly Mock<IVoiceClient> Client = new();
        public readonly Mock<ICoopOptionsStore> Store = new();
        public CoopOptionsData Saved = new();
        public readonly VoiceSection Section;
        public Harness(VoiceSettings? settings = null)
        {
            Client.Setup(x => x.Microphones()).Returns(new[] { "System default", "USB microphone" });
            Store.Setup(x => x.LoadOrDefault()).Returns(() => Saved);
            Store.Setup(x => x.Save(It.IsAny<CoopOptionsData>())).Callback<CoopOptionsData>(data => Saved = data);
            Section = new VoiceSection(Client.Object, settings ?? new VoiceSettings(), Store.Object);
        }
        public VoiceSettings Settings => VoiceOptionsTabProvider.Load(Saved);
        public void Dispose() => Section.OnFinalize();
    }

    [Fact]
    public void EnableDefaultsToTrueAndImmediatelyPersistsWithoutLosingSpeakingModeOrKey()
    {
        Assert.True(new VoiceSettings().Validated().Enabled);
        Assert.True(VoiceOptionsTabProvider.Load(new CoopOptionsData()).Enabled);
        var legacy = new VoiceSettings { Activation = VoiceActivation.Disabled }.Validated();
        Assert.False(legacy.Enabled);
        Assert.Equal(VoiceActivation.PushToTalk, legacy.Activation);
        using var h = new Harness(new VoiceSettings { Activation = VoiceActivation.VoiceActivity, PushToTalkKey = InputKey.F12 });
        h.Section.Enabled = false;
        Assert.False(h.Section.IsKeybindingEnabled);
        Assert.False(h.Section.IsVoiceActivation);
        Assert.False(h.Settings.Enabled);
        Assert.Equal(VoiceActivation.VoiceActivity, h.Settings.Activation);
        Assert.Equal(InputKey.F12, h.Settings.PushToTalkKey);
        h.Client.Verify(x => x.Apply(It.Is<VoiceSettings>(s => !s.Enabled)), Times.Once);
        h.Section.Enabled = true;
        Assert.True(h.Settings.Enabled);
    }

    [Fact]
    public void EveryControlSavesAndAppliesOnceWithoutWaitingForMenuClose()
    {
        using var h = new Harness();
        int changes = 0;
        void Change(Action change, Action<VoiceSettings> check)
        {
            change();
            changes++;
            check(h.Settings);
            h.Store.Verify(x => x.Save(It.IsAny<CoopOptionsData>()), Times.Exactly(changes));
            h.Client.Verify(x => x.Apply(It.IsAny<VoiceSettings>()), Times.Exactly(changes));
        }
        Change(() => h.Section.ActivationSelector.SelectedIndex = 1, s => Assert.Equal(VoiceActivation.VoiceActivity, s.Activation));
        Assert.True(h.Section.IsVoiceActivation);
        Change(() => h.Section.MicrophoneSelector.SelectedIndex = 1, s => { Assert.Equal(0, s.Microphone); Assert.Equal("USB microphone", s.MicrophoneName); });
        Change(() => h.Section.Muted = true, s => Assert.True(s.Muted));
        Change(() => h.Section.Deafened = true, s => Assert.True(s.Deafened));
        Change(() => h.Section.Volume = 0.4f, s => Assert.Equal(0.4f, s.Volume));
        Change(() => h.Section.Sensitivity = 0.07f, s => Assert.Equal(0.07f, s.Sensitivity));
        Change(() => h.Section.Enabled = false, s => Assert.False(s.Enabled));
        Assert.True(h.Section.ShowTalkingPlayers);
        Change(() => h.Section.ShowTalkingPlayers = false, s => Assert.False(s.ShowTalkingPlayers));
        Change(() => h.Section.PushToTalkKey.Set(InputKey.F12), s => Assert.Equal(InputKey.F12, s.PushToTalkKey));
        Change(h.Section.ExecuteResetKey, s => Assert.Equal(InputKey.Q, s.PushToTalkKey));
        h.Section.OnFinalize();
        h.Store.Verify(x => x.Save(It.IsAny<CoopOptionsData>()), Times.Exactly(changes));
    }

    [Fact]
    public void InitializationRefreshStatusAndSameValuesDoNotSaveOrApply()
    {
        var original = new VoiceSettings { Microphone = 0, MicrophoneName = "Previously saved microphone" };
        using var h = new Harness(original);
        h.Section.ExecuteRetry();
        h.Client.Raise(x => x.StatusChanged += null, Array.Empty<object>());
        h.Section.Enabled = true;
        h.Section.Muted = false;
        h.Section.Deafened = false;
        h.Section.ShowTalkingPlayers = true;
        h.Section.Volume = 1;
        h.Section.Sensitivity = original.Sensitivity;
        h.Section.ActivationSelector.SelectedIndex = 0;
        h.Section.PushToTalkKey.Set(InputKey.Q);
        h.Section.ExecuteResetKey();
        h.Section.PushToTalkKey.ExecuteRevert();
        h.Store.Verify(x => x.Save(It.IsAny<CoopOptionsData>()), Times.Never);
        h.Client.Verify(x => x.Apply(It.IsAny<VoiceSettings>()), Times.Never);
        h.Section.Muted = true;
        Assert.Equal("Previously saved microphone", h.Settings.MicrophoneName);
        Assert.False(original.Muted);
    }

    [Fact]
    public void NativeKeyCommitsImmediatelyAndRevertCannotUndoAnAcceptedKey()
    {
        var original = new VoiceSettings { PushToTalkKey = InputKey.F11 };
        using var h = new Harness(original);
        Assert.IsAssignableFrom<GameKeyOptionVM>(h.Section.PushToTalkKey);
        Assert.Equal("F11", h.Section.PushToTalkKey.OptionValueText);
        h.Section.PushToTalkKey.Set(InputKey.F12);
        Assert.Equal(InputKey.F12, h.Settings.PushToTalkKey);
        Assert.Equal("F12", h.Section.PushToTalkKey.OptionValueText);
        Assert.False(h.Section.PushToTalkKey.IsChanged);
        Assert.Equal(InputKey.F11, original.PushToTalkKey);
        h.Section.PushToTalkKey.ExecuteRevert();
        Assert.Equal(InputKey.F12, h.Section.PushToTalkKey.CurrentKey.InputKey);
        h.Section.OnFinalize();
        Assert.Equal(InputKey.F12, h.Settings.PushToTalkKey);
        h.Client.Verify(x => x.Apply(It.IsAny<VoiceSettings>()), Times.Once);
        h.Store.Verify(x => x.Save(It.IsAny<CoopOptionsData>()), Times.Once);
    }

    private sealed class StagedSection : CoopOptionsSectionVM
    {
        public override string Id => "Staged";
        public VoiceSettings Value = new();
        public override void Apply(string tabId, CoopOptionsData options) => options.SetSection(tabId, Id, Value);
    }

    private sealed class StagedProvider : ICoopOptionsTabProvider
    {
        public readonly StagedSection Section = new();
        public string Id => "Other";
        public bool IsAvailable(ModOptions options) => true;
        public CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker broker, Action<CoopOptionsTabVM> select)
            => new(Id, "Other", new[] { Section }, select);
    }

    [Fact]
    public void VoiceUsesLatestStoreWithoutPersistingOtherTabsStagedEditsAndHidesApply()
    {
        using var h = new Harness();
        using var broker = new MessageBroker();
        var other = new StagedProvider();
        var options = new CoopOptionsVM(h.Store.Object, broker,
            new ICoopOptionsTabProvider[] { new VoiceOptionsTabProvider(h.Client.Object, h.Store.Object), other }, default, () => { });
        try
        {
            Assert.False(options.IsApplyButtonVisible);
            other.Section.Value.Volume = 0.2f;
            h.Saved = new CoopOptionsData();
            h.Saved.SetSection("Other", "Staged", new VoiceSettings { Volume = 0.8f });
            var voice = Assert.IsType<VoiceSection>(Assert.Single(options.VoiceTab.Sections));
            voice.Volume = 0.4f;
            Assert.Equal(0.8f, h.Saved.GetSectionOrDefault("Other", "Staged", new VoiceSettings()).Volume);
            options.ActionApply();
            h.Store.Verify(x => x.Save(It.IsAny<CoopOptionsData>()), Times.Once);
            options.Tabs[1].ExecuteSelection();
            Assert.True(options.IsApplyButtonVisible);
            options.ActionApply();
            Assert.Equal(0.2f, h.Saved.GetSectionOrDefault("Other", "Staged", new VoiceSettings()).Volume);
            Assert.Equal(0.4f, h.Settings.Volume);
        }
        finally { options.OnFinalize(); }
    }

    [Fact]
    public void PersistedChangesSurviveClosingAndReopeningWithoutAnotherSave()
    {
        string path = Path.Combine(Path.GetTempPath(), "voice-options-" + Guid.NewGuid() + ".json");
        try
        {
            using var h = new Harness();
            using var broker = new MessageBroker();
            var store = new CoopOptionsStore(path);
            var provider = new VoiceOptionsTabProvider(h.Client.Object, store);
            var tab = provider.CreateTab(store.LoadOrDefault(), broker, _ => { });
            var section = Assert.IsType<VoiceSection>(Assert.Single(tab.Sections));
            section.PushToTalkKey.Set(InputKey.F12);
            section.ShowTalkingPlayers = false;
            tab.OnFinalize();
            h.Client.Invocations.Clear();
            var reopened = provider.CreateTab(store.LoadOrDefault(), broker, _ => { });
            var reloaded = Assert.IsType<VoiceSection>(Assert.Single(reopened.Sections));
            Assert.Equal(InputKey.F12, reloaded.PushToTalkKey.CurrentKey.InputKey);
            Assert.False(reloaded.ShowTalkingPlayers);
            h.Client.Verify(x => x.Apply(It.IsAny<VoiceSettings>()), Times.Never);
            reopened.OnFinalize();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Theory]
    [InlineData("load")]
    [InlineData("save")]
    [InlineData("apply")]
    public void SettingsFailuresAreNonfatalAndNextChangeRetries(string failure)
    {
        using var h = new Harness();
        if (failure == "load") h.Store.Setup(x => x.LoadOrDefault()).Throws(new IOException("locked"));
        if (failure == "save") h.Store.Setup(x => x.Save(It.IsAny<CoopOptionsData>())).Throws(new IOException("locked"));
        if (failure == "apply") h.Client.Setup(x => x.Apply(It.IsAny<VoiceSettings>())).Throws(new InvalidOperationException("unavailable"));
        h.Section.PushToTalkKey.Set(InputKey.F12);
        Assert.Contains("could not be saved", h.Section.StatusText);
        h.Section.PushToTalkKey.ExecuteRevert();
        Assert.Equal(InputKey.F12, h.Section.PushToTalkKey.CurrentKey.InputKey);
        h.Store.Setup(x => x.LoadOrDefault()).Returns(() => h.Saved);
        h.Store.Setup(x => x.Save(It.IsAny<CoopOptionsData>())).Callback<CoopOptionsData>(data => h.Saved = data);
        h.Client.Setup(x => x.Apply(It.IsAny<VoiceSettings>()));
        h.Client.SetupGet(x => x.Status).Returns("Ready");
        h.Section.Volume = 0.5f;
        Assert.Equal(InputKey.F12, h.Settings.PushToTalkKey);
        Assert.Empty(h.Section.StatusText);
    }

    [Fact]
    public void TestButtonTogglesAndStatusEventsSampleMeterWithoutSaving()
    {
        using var h = new Harness();
        bool testing = false;
        float level = 0;
        h.Client.SetupGet(x => x.IsTestingMicrophone).Returns(() => testing);
        h.Client.SetupGet(x => x.InputLevel).Returns(() => level);
        h.Client.Setup(x => x.TestMicrophone(It.IsAny<bool>())).Callback<bool>(value => testing = value);
        Assert.Equal("Test microphone", h.Section.TestText);
        h.Section.ExecuteTest();
        Assert.Equal("Stop", h.Section.TestText);
        h.Section.ExecuteTest();
        Assert.Equal("Test microphone", h.Section.TestText);
        level = 0.9f;
        Assert.Equal(0, h.Section.InputLevel);
        h.Client.Raise(x => x.StatusChanged += null, Array.Empty<object>());
        Assert.Equal(0.9f, h.Section.InputLevel);
        Assert.True(h.Section.InputLevelLow);
        Assert.True(h.Section.InputLevelPeak);
        level = 0;
        h.Client.Raise(x => x.StatusChanged += null, Array.Empty<object>());
        Assert.Equal(0, h.Section.InputLevel);
        Assert.False(h.Section.InputLevelLow);
        h.Section.ExecuteTest();
        h.Section.OnFinalize();
        Assert.False(testing);
        h.Client.VerifyRemove(x => x.StatusChanged -= It.IsAny<Action>(), Times.Once);
        h.Store.Verify(x => x.Save(It.IsAny<CoopOptionsData>()), Times.Never);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"VoiceTab\":{\"VoiceSection\":{\"Volume\":0.5}}}")]
    public void MissingKeyAndTalkingPlayersInOldLocalSettingsUseDefaults(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), "voice-legacy-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, json);
            var settings = VoiceOptionsTabProvider.Load(new CoopOptionsStore(path).LoadOrDefault()).Validated();
            Assert.Equal(InputKey.Q, settings.PushToTalkKey);
            Assert.True(settings.ShowTalkingPlayers);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(InputKey.Invalid)]
    [InlineData(InputKey.Escape)]
    [InlineData(InputKey.ControllerLRight)]
    [InlineData(InputKey.MouseScrollUp)]
    [InlineData((InputKey)99999)]
    public void InvalidKeyboardBindingsFallBackToQ(InputKey key)
    {
        Assert.Equal(InputKey.Q, new VoiceSettings { PushToTalkKey = key }.Validated().PushToTalkKey);
    }

    [Fact]
    public void InvalidLocalSettingsAndServerRangesUseSafeValues()
    {
        var local = new VoiceSettings { Activation = (VoiceActivation)99, Microphone = int.MaxValue,
            Sensitivity = float.NaN, Volume = float.PositiveInfinity }.Validated();
        Assert.Equal(VoiceActivation.PushToTalk, local.Activation);
        Assert.Equal(255, local.Microphone);
        Assert.Equal(0.02f, local.Sensitivity);
        Assert.Equal(1, local.Volume);
        var invalid = new VoiceConfigData { SceneFullVolumeDistance = 100, SceneMaximumDistance = 5 }.ToRanges();
        Assert.True(invalid.IsValid);
        Assert.Equal(50, invalid.SceneMaximum);
        var configured = new VoiceConfigData { MapFullVolumeDistance = 3, MapMaximumDistance = 9 }.ToRanges();
        Assert.Equal(9, configured.MapMaximum);
    }
}
