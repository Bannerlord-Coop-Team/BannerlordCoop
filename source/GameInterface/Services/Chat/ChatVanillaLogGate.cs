using System;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Chat;

/// <summary>Hides the vanilla bottom-left chat log while co-op chat owns that role.</summary>
public interface IChatVanillaLogGate
{
    void Activate();
    void Deactivate();
}

/// <inheritdoc cref="IChatVanillaLogGate"/>
public sealed class ChatVanillaLogGate : IChatVanillaLogGate
{
    public static bool IsActive { get; private set; }

    public void Activate()
    {
        IsActive = true;
        SuspendIfPresent();
    }

    public void Deactivate()
    {
        IsActive = false;
        ResumeIfPresent();
    }

    internal static void SuspendIfPresent()
    {
        var current = GauntletChatLogView.Current;
        if (current?.Layer == null) return;

        ScreenManager.SetSuspendLayer(current.Layer, true);
    }

    internal static void ResumeIfPresent()
    {
        var current = GauntletChatLogView.Current;
        if (current?.Layer == null) return;

        ScreenManager.SetSuspendLayer(current.Layer, false);
    }
}