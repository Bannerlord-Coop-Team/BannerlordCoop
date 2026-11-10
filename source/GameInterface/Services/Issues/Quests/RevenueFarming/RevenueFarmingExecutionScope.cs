using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Patches;
using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

internal interface IRevenueFarmingExecutionScope
{
    RevenueFarmingExecutionScope.Scope Current { get; }
    IDisposable Enter(RevenueFarmingSession session, bool publish = true, RevenueFarmingAction? action = null);
}

internal sealed class RevenueFarmingExecutionScope : IRevenueFarmingExecutionScope, IDisposable
{
    private readonly ThreadLocal<Scope> current = new();
    private readonly IMessageBroker broker;

    public RevenueFarmingExecutionScope(IMessageBroker broker)
    {
        this.broker = broker;
    }

    public Scope Current => current.Value;

    public IDisposable Enter(RevenueFarmingSession session, bool publish = true, RevenueFarmingAction? action = null) =>
        new Scope(this, session, publish, action);

    public void Dispose() => current.Dispose();

    internal sealed class Scope : IDisposable
    {
        private readonly RevenueFarmingExecutionScope execution;
        private readonly Scope previous;
        private readonly MainHeroSubstitutionScope ownerScope;
        private readonly bool publish;
        private readonly string initialFingerprint = null;

        internal RevenueFarmingSession Session { get; }
        internal RevenueFarmingAction? Action { get; }
        internal string Menu { get; set; }
        internal bool FinishEncounter { get; set; }

        internal Scope(RevenueFarmingExecutionScope execution, RevenueFarmingSession session, bool publish, RevenueFarmingAction? action)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            this.execution = execution;
            Session = session;
            Action = action;
            previous = execution.Current;
            this.publish = publish && previous == null && ModInformation.IsServer;
            if (ModInformation.IsServer)
            {
                if (session.PlayerParty == null) throw new InvalidOperationException("Revenue Farming owner has no recorded party.");
                initialFingerprint = Fingerprint();
                ownerScope = new MainHeroSubstitutionScope(session.PlayerHero, session.PlayerParty);
            }
            execution.current.Value = this;
        }

        public void Dispose()
        {
            var changed = false;
            try
            {
                if (publish && (Session.Quest.IsOngoing || Menu != null || FinishEncounter) &&
                    (Menu != null || FinishEncounter || initialFingerprint != Fingerprint()))
                {
                    if (Session.Incident != null && Session.Quest.RevenueVillages.Any(village =>
                        village.Village.Settlement == Session.CollectionSettlement && village.GetIsCompleted()))
                    {
                        Session.Incident = null;
                        Session.Quest.CollectingRevenues = false;
                        Menu = "village";
                    }
                    changed = true;
                }
            }
            finally
            {
                execution.current.Value = previous;
                ownerScope?.Dispose();
            }
            if (changed) execution.broker.Publish(this, new RevenueFarmingChanged(Session, Menu, FinishEncounter));
        }

        private string Fingerprint()
        {
            var quest = Session.Quest;
            var builder = new StringBuilder();
            builder.Append(quest.IsOngoing).Append('|')
                .Append(quest.CollectingRevenues).Append('|')
                .Append(quest._allRevenuesAreCollected).Append('|')
                .Append(quest._totalRequestedDenars).Append('|')
                .Append(quest._questProgressLog?.CurrentProgress ?? -1).Append('|')
                .Append(quest._journalEntries.Count).Append('|')
                .Append(Session.Incident).Append('|')
                .Append(Session.AwaitingPayment).Append('|')
                .Append(Session.CollectionSettlement?.StringId);
            foreach (var village in quest.RevenueVillages)
            {
                builder.Append('|').Append(village.CollectedAmount)
                    .Append(',').Append(village.HourlyGain)
                    .Append(',').Append(village._isDone)
                    .Append(',').Append(village.EventOccurred)
                    .Append(',').Append(village.IsRaided)
                    .Append(',').Append(village._customProgress);
            }
            return builder.ToString();
        }
    }
}

public readonly struct RevenueFarmingChanged : IEvent
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
