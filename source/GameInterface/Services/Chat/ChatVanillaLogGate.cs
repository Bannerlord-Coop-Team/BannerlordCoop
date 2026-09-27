using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Chat;

/// <summary>Hides the vanilla bottom-left chat log while co-op chat owns that role.</summary>
public interface IChatVanillaLogGate
{
    void Activate();
    void Deactivate();
    void SetReplacementVisible(bool visible);
}

/// <inheritdoc cref="IChatVanillaLogGate"/>
public sealed class ChatVanillaLogGate : IChatVanillaLogGate
{
    public static bool IsActive { get; private set; }

    /// <summary>True when the co-op overlay is on screen and replacing the vanilla log.</summary>
    public static bool IsReplacementVisible { get; private set; }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
        IsReplacementVisible = false;
        ResumeIfPresent();
    }

    public void SetReplacementVisible(bool visible)
    {
        if (!IsActive)
        {
            IsReplacementVisible = false;
            ResumeIfPresent();
            return;
        }

        IsReplacementVisible = visible;
        // Re-assert every tick: another system can flip the layer after our last toggle.
        if (visible)
            SuspendIfPresent();
        else
            ResumeIfPresent();
    }

    internal static void SuspendIfPresent()
    {
        if (!IsActive || !IsReplacementVisible) return;

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