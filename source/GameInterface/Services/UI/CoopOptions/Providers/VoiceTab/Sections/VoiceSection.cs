using Common.Voice;
using Common.Logging;
using Serilog;
using GameInterface.Services.Voice;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Core.ViewModelCollection.Selector;
using System.Linq;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

namespace GameInterface.Services.UI.CoopOptions.Providers.VoiceTab.Sections;

public sealed class VoiceSection : CoopOptionsSectionVM
{
    public const string SectionId = "VoiceSection";
    private static readonly ILogger Logger = LogManager.GetLogger<VoiceSection>();
    private readonly IVoiceClient client;
    private readonly ICoopOptionsStore store;
    private readonly VoiceSettings settings;
    private IReadOnlyList<string> microphones;
    private bool refreshing;
    private bool finalized;
    private string settingsError;
    private float inputLevel;

    public VoiceSection(IVoiceClient client, VoiceSettings settings, ICoopOptionsStore store)
    {
        this.client = client;
        this.store = store;
        this.settings = settings.Validated();
        microphones = client.Microphones();
        ActivationSelector = new SelectorVM<SelectorItemVM>(
            new[] { "Push to talk", "Voice activation" }, (int)this.settings.Activation, null);
        ActivationSelector.SetOnChangeAction(SelectActivation);
        MicrophoneSelector = new SelectorVM<SelectorItemVM>(MicrophoneChoices(), this.settings.Microphone + 1, null);
        MicrophoneSelector.SetOnChangeAction(SelectMicrophone);
        client.StatusChanged += RefreshStatus;
        PushToTalkKey = new VoicePushToTalkKeyVM(this.settings.PushToTalkKey, key => KeybindRequested?.Invoke(key));
        PushToTalkKey.KeyChanged += SelectKey;
        Input.OnGamepadActiveStateChanged += RefreshKeybindingEnabled;
    }

    public event Action<KeyOptionVM> KeybindRequested;
    [DataSourceProperty] public VoicePushToTalkKeyVM PushToTalkKey { get; }
    [DataSourceProperty] public bool IsKeybindingEnabled => settings.Enabled && !Input.IsGamepadActive;
    public string ResetKeyText => "Reset to Default";
    public void ExecuteResetKey() => PushToTalkKey.Set(InputKey.Q);
    private void RefreshKeybindingEnabled() => OnPropertyChanged(nameof(IsKeybindingEnabled));

    public override string Id => SectionId;
    public override bool CanApply => false;
    public string TitleText => "Proximity voice";
    public string DescriptionText => "Talk to nearby players. Changes are saved immediately.";
    public string EnabledText => "Enable voice chat";
    public string InputText => "Input";
    public string PlaybackText => "Playback";
    public string MicrophoneTestText => "Microphone test";
    public string RetryText => "Retry / Refresh";
    public string ActivationLabel => "Speaking mode";
    public string MicrophoneLabel => "Microphone";
    public string MuteLabel => "Mute microphone";
    public string DeafenLabel => "Deafen (also mutes microphone)";
    public string SensitivityText => "Activation threshold";
    public string VolumeText => "Voice volume";
    public string ShowTalkingPlayersText => "Show talking players";
    public string InputLevelText => "Input level";
    public string InputPeakText => "Peak";
    [DataSourceProperty] public float InputLevel => inputLevel;
    [DataSourceProperty] public bool InputLevelLow => InputLevel >= 0.01f;
    [DataSourceProperty] public bool InputLevelMedium => InputLevel >= 0.1f;
    [DataSourceProperty] public bool InputLevelHigh => InputLevel >= 0.25f;
    [DataSourceProperty] public bool InputLevelVeryHigh => InputLevel >= 0.5f;
    [DataSourceProperty] public bool InputLevelPeak => InputLevel >= 0.85f;
    [DataSourceProperty] public string TestText => client.IsTestingMicrophone ? "Stop" : "Test microphone";
    [DataSourceProperty] public string StatusText
    {
        get
        {
            var status = settingsError ?? client.Status;
            return status == "Ready" ? string.Empty : status;
        }
    }
    [DataSourceProperty] public SelectorVM<SelectorItemVM> ActivationSelector { get; }
    [DataSourceProperty] public SelectorVM<SelectorItemVM> MicrophoneSelector { get; }
    [DataSourceProperty] public bool IsVoiceActivation => settings.Enabled && settings.Activation == VoiceActivation.VoiceActivity;
    [DataSourceProperty] public bool Enabled
    {
        get => settings.Enabled;
        set
        {
            if (settings.Enabled == value) return;
            settings.Enabled = value;
            SaveChanges();
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(IsVoiceActivation));
            OnPropertyChanged(nameof(IsKeybindingEnabled));
        }
    }
    [DataSourceProperty] public bool Muted
    {
        get => settings.Muted;
        set
        {
            if (settings.Muted == value) return;
            settings.Muted = value;
            SaveChanges();
            OnPropertyChanged(nameof(Muted));
        }
    }
    [DataSourceProperty] public bool Deafened
    {
        get => settings.Deafened;
        set
        {
            if (settings.Deafened == value) return;
            settings.Deafened = value;
            SaveChanges();
            OnPropertyChanged(nameof(Deafened));
        }
    }
    [DataSourceProperty] public bool ShowTalkingPlayers
    {
        get => settings.ShowTalkingPlayers;
        set
        {
            if (settings.ShowTalkingPlayers == value) return;
            settings.ShowTalkingPlayers = value;
            SaveChanges();
            OnPropertyChanged(nameof(ShowTalkingPlayers));
        }
    }
    [DataSourceProperty]
    public float Sensitivity
    {
        get => settings.Sensitivity;
        set
        {
            if (float.IsNaN(value)) return;
            value = Math.Max(0.001f, Math.Min(1, value));
            if (settings.Sensitivity == value) return;
            settings.Sensitivity = value;
            SaveChanges();
            OnPropertyChanged(nameof(Sensitivity));
        }
    }
    [DataSourceProperty]
    public float Volume
    {
        get => settings.Volume;
        set
        {
            if (float.IsNaN(value)) return;
            value = Math.Max(0, Math.Min(1, value));
            if (settings.Volume == value) return;
            settings.Volume = value;
            SaveChanges();
            OnPropertyChanged(nameof(Volume));
        }
    }

    private IEnumerable<string> MicrophoneChoices()
    {
        // Keep an unavailable saved device visible instead of silently selecting another microphone.
        return microphones.Concat(Enumerable.Repeat("Unavailable", Math.Max(0, settings.Microphone + 2 - microphones.Count)));
    }

    private void SelectActivation(SelectorVM<SelectorItemVM> selector)
    {
        if (refreshing || selector.SelectedIndex < 0 || selector.SelectedIndex > 1 ||
            settings.Activation == (VoiceActivation)selector.SelectedIndex) return;
        settings.Activation = (VoiceActivation)selector.SelectedIndex;
        SaveChanges();
        OnPropertyChanged(nameof(IsVoiceActivation));
    }

    private void SelectKey(InputKey key)
    {
        if (settings.PushToTalkKey == key) return;
        settings.PushToTalkKey = key;
        PushToTalkKey.Apply();
        SaveChanges();
    }

    private void SelectMicrophone(SelectorVM<SelectorItemVM> selector)
    {
        if (refreshing) return;
        int index = selector.SelectedIndex - 1;
        if (index < -1 || index + 1 >= microphones.Count) return;
        string name = index < 0 ? null : microphones[index + 1];
        if (settings.Microphone == index && settings.MicrophoneName == name) return;
        settings.Microphone = index;
        settings.MicrophoneName = name;
        SaveChanges();
    }
    public void ExecuteTest()
    {
        if (settings.Enabled) client.TestMicrophone(!client.IsTestingMicrophone);
        RefreshStatus();
    }
    public void ExecuteRetry()
    {
        microphones = client.Microphones();
        client.Retry();
        refreshing = true;
        try
        {
            MicrophoneSelector.Refresh(MicrophoneChoices(), settings.Microphone + 1, null);
            MicrophoneSelector.SetOnChangeAction(SelectMicrophone);
        }
        finally { refreshing = false; }
        RefreshStatus();
    }
    private void RefreshStatus()
    {
        inputLevel = client.InputLevel;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(TestText));
        OnPropertyChanged(nameof(InputLevel));
        OnPropertyChanged(nameof(InputLevelLow));
        OnPropertyChanged(nameof(InputLevelMedium));
        OnPropertyChanged(nameof(InputLevelHigh));
        OnPropertyChanged(nameof(InputLevelVeryHigh));
        OnPropertyChanged(nameof(InputLevelPeak));
    }
    public override void Apply(string tabId, CoopOptionsData options)
    {
        settings.PushToTalkKey = PushToTalkKey.CurrentKey.InputKey;
        options.SetSection(tabId, Id, settings.Validated());
    }
    private void SaveChanges()
    {
        if (refreshing || finalized) return;
        try
        {
            client.Apply(settings);
            var options = store.LoadOrDefault();
            options.SetSection(VoiceOptionsTabProvider.TabId, Id, settings.Validated());
            store.Save(options);
            settingsError = null;
        }
        catch (Exception ex)
        {
            settingsError = "Voice settings could not be saved. Change a setting to retry.";
            Logger.Warning(ex, "Voice settings update failed; gameplay continues");
        }
        RefreshStatus();
    }
    public override void OnFinalize()
    {
        if (finalized) return;
        finalized = true;
        PushToTalkKey.KeyChanged -= SelectKey;
        Input.OnGamepadActiveStateChanged -= RefreshKeybindingEnabled;
        PushToTalkKey.OnFinalize();
        ActivationSelector.OnFinalize();
        MicrophoneSelector.OnFinalize();
        KeybindRequested = null;
        client.StatusChanged -= RefreshStatus;
        client.TestMicrophone(false);
        base.OnFinalize();
    }
}
