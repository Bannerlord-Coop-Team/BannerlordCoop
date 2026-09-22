using Common;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Chat;

/// <summary>Forwards vanilla InformationManager lines into the co-op chat feed.</summary>
public interface IChatEventLog : IDisposable
{
    void Start(Action<string, Color> receiveEvent);
    void Stop();
}

/// <inheritdoc cref="IChatEventLog"/>
public sealed class ChatEventLog : IChatEventLog
{
    public const string DefaultCategory = "Default";
    public const string CombatCategory = "Combat";
    public const string BarkCategory = "Bark";

    private Action<string, Color> receiveEvent;
    private bool started;

    public void Start(Action<string, Color> receiveEvent)
    {
        if (receiveEvent == null) throw new ArgumentNullException(nameof(receiveEvent));
        if (started) return;

        this.receiveEvent = receiveEvent;
        InformationManager.DisplayMessageInternal += OnDisplayMessageReceived;
        started = true;
    }

    public void Stop()
    {
        if (!started) return;

        InformationManager.DisplayMessageInternal -= OnDisplayMessageReceived;
        receiveEvent = null;
        started = false;
    }

    public void Dispose()
    {
        Stop();
    }

    internal static bool ShouldInclude(string category, bool reportDamage, bool reportBark)
    {
        if (string.Equals(category, CombatCategory, StringComparison.Ordinal))
            return reportDamage;
        if (string.Equals(category, BarkCategory, StringComparison.Ordinal))
            return reportBark;
        return true;
    }

    private void OnDisplayMessageReceived(InformationMessage message)
    {
        if (message == null || string.IsNullOrEmpty(message.Information)) return;

        string category = string.IsNullOrEmpty(message.Category) ? DefaultCategory : message.Category;
        if (!ShouldInclude(category, BannerlordConfig.ReportDamage, BannerlordConfig.ReportBark))
            return;

        string text = message.Information;
        Color color = message.Color;
        var sink = receiveEvent;
        GameThread.RunSafe(
            () => sink?.Invoke(text, color),
            context: nameof(ChatEventLog));
    }
}
