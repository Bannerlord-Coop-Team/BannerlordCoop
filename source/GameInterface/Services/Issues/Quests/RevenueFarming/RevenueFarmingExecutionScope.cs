using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Patches;
using System;
using System.Linq;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

internal sealed class RevenueFarmingExecutionScope : IDisposable
{
    [ThreadStatic] internal static RevenueFarmingExecutionScope Current;

    private readonly RevenueFarmingExecutionScope previous;
    private readonly MainHeroSubstitutionScope ownerScope;
    private readonly bool publish;

    internal RevenueFarmingSession Session { get; }
    internal string Menu { get; set; }
    internal bool FinishEncounter { get; set; }

    internal RevenueFarmingExecutionScope(RevenueFarmingSession session, bool publish = true)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        Session = session;
        previous = Current;
        this.publish = publish && previous == null && ModInformation.IsServer;
        if (ModInformation.IsServer)
        {
            if (session.PlayerParty == null) throw new InvalidOperationException("Revenue Farming owner has no recorded party.");
            ownerScope = new MainHeroSubstitutionScope(session.PlayerHero, session.PlayerParty);
        }
        Current = this;
    }

    public void Dispose()
    {
        try
        {
            if (publish && (Session.Quest.IsOngoing || Menu != null || FinishEncounter))
            {
                if (Session.Incident != null && Session.Quest.RevenueVillages.Any(village =>
                    village.Village.Settlement == Session.CollectionSettlement && village.GetIsCompleted()))
                {
                    Session.Incident = null;
                    Session.Quest.CollectingRevenues = false;
                    Menu = "village";
                }
                MessageBroker.Instance.Publish(this, new RevenueFarmingChanged(Session, Menu, FinishEncounter));
            }
        }
        finally
        {
            Current = previous;
            ownerScope?.Dispose();
        }
    }
}

internal readonly struct RevenueFarmingChanged : IEvent
{
    internal RevenueFarmingSession Session { get; }
    internal string Menu { get; }
    internal bool FinishEncounter { get; }
    internal bool IsFinalizing { get; }
    internal bool RefreshPresentation { get; }

    internal RevenueFarmingChanged(RevenueFarmingSession session, string menu = null, bool finishEncounter = false,
        bool isFinalizing = false, bool refreshPresentation = false)
    {
        Session = session;
        Menu = menu;
        FinishEncounter = finishEncounter;
        IsFinalizing = isFinalizing;
        RefreshPresentation = refreshPresentation;
    }
}
