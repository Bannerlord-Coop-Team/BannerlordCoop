using GameInterface.Services.UI.CoopOptions.Providers.VoiceTab.Sections;
using GameInterface.Services.Voice;
using System;
using System.Collections.Generic;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.CoopOptions;

public interface ICoopOptionsKeybinding : IDisposable
{
    void Attach(ScreenBase owner, CoopOptionsVM options, Action restoreFocus);
    void Tick();
    void Cancel();
}

public sealed class CoopOptionsKeybinding : ICoopOptionsKeybinding
{
    private readonly ICoopKeybindingPopupFactory factory;
    private readonly IVoiceClient voice;
    private readonly IVoiceWindowFocus window;
    private readonly List<VoiceSection> sections = new();
    private ICoopKeybindingPopup popup;
    private KeyOptionVM current;
    private Action restoreFocus;
    private bool capturing;

    public CoopOptionsKeybinding(ICoopKeybindingPopupFactory factory, IVoiceClient voice, IVoiceWindowFocus window)
    {
        this.factory = factory;
        this.voice = voice;
        this.window = window;
    }

    public void Attach(ScreenBase owner, CoopOptionsVM options, Action restoreFocus)
    {
        this.restoreFocus = restoreFocus;
        popup = factory.Create(SetKey, owner);
        foreach (var tab in options.Tabs)
            foreach (var section in tab.Sections)
                if (section is VoiceSection voiceSection)
                {
                    sections.Add(voiceSection);
                    voiceSection.KeybindRequested += Request;
                }
    }

    private void Request(KeyOptionVM key)
    {
        if (popup == null || Input.IsGamepadActive || !window.IsFocused) return;
        current = key;
        capturing = true;
        voice.SetKeybindCapture(true);
        try { popup.Toggle(true); }
        catch { EndCapture(); throw; }
    }

    private void SetKey(Key key)
    {
        if (new VoiceSettings().IsSupportedPushToTalkKey(key.InputKey)) current?.Set(key.InputKey);
        EndCapture();
    }

    public void Tick()
    {
        if (!capturing) return;
        if (!window.IsFocused) { EndCapture(); return; }
        popup.Tick();
        if (!popup.IsActive) EndCapture();
    }

    public void Cancel() => EndCapture();

    private void EndCapture()
    {
        try { popup?.Toggle(false); }
        finally
        {
            current = null;
            if (capturing)
            {
                capturing = false;
                voice.SetKeybindCapture(false);
                restoreFocus?.Invoke();
            }
        }
    }

    public void Dispose()
    {
        restoreFocus = null;
        EndCapture();
        foreach (var section in sections) section.KeybindRequested -= Request;
        sections.Clear();
        popup = null;
    }
}
