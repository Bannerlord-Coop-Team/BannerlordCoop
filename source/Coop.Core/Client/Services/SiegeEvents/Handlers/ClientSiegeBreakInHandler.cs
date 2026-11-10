using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Coop.Core.Client.Services.SiegeEvents.Handlers;

internal class ClientSiegeBreakInHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientSiegeBreakInHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISiegeBreakOut breakOut;
    private readonly ISiegeCasualtyDecoder casualtyDecoder;
    private const int MaxSupersededRequests = 8;
    private Pending pending;
    // Attempts replaced before their reply came, so a late paid reply still tells the player what it cost.
    // The reply carries no siege, so the attempt is kept to compare it with a newer one.
    private readonly List<Pending> superseded = new List<Pending>();

    private sealed class Pending
    {
        public NetworkRequestBreakIn Request;
        public MobileParty Party;
        public Settlement Settlement;
        public SiegeEvent Siege;
        public PlayerEncounter Encounter;
        public string MenuId;
        // Troops an older attempt for the same siege already paid, told here if this attempt does not get in.
        public int UntoldLosses;

        public bool IsCurrent() => MobileParty.MainParty == Party && Party.CurrentSettlement == null &&
            PlayerEncounter.EncounterSettlement == Settlement && Settlement.SiegeEvent == Siege &&
            ReferenceEquals(PlayerEncounter.Current, Encounter) &&
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId == MenuId;
    }

    public ClientSiegeBreakInHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, ISiegeBreakOut breakOut, ISiegeCasualtyDecoder casualtyDecoder)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.breakOut = breakOut;
        this.casualtyDecoder = casualtyDecoder;
        messageBroker.Subscribe<BreakInAttempted>(HandleAttempt);
        messageBroker.Subscribe<NetworkBreakInResult>(HandleResult);
    }

    private void HandleAttempt(MessagePayload<BreakInAttempted> payload)
    {
        if (pending != null && pending.IsCurrent())
        {
            network.SendAll(pending.Request);
            return;
        }
        Supersede(pending);
        SetPending(null);
        var party = payload.What.Party;
        var settlement = payload.What.Settlement;
        var siege = settlement?.SiegeEvent;
        if (party != MobileParty.MainParty || siege == null || PlayerEncounter.Current == null) return;
        if (!objectManager.TryGetHandleWithLogging(party, out var partyId) ||
            !objectManager.TryGetHandleWithLogging(settlement, out var settlementId) ||
            !objectManager.TryGetHandleWithLogging(siege, out var siegeId)) return;

        SetPending(new Pending
        {
            Request = new NetworkRequestBreakIn(Guid.NewGuid().ToString(), partyId, settlementId, siegeId),
            Party = party,
            Settlement = settlement,
            Siege = siege,
            Encounter = PlayerEncounter.Current,
            MenuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId,
        });
        // Losses an older attempt was holding move to the newest one, so they are told once even if its reply never comes.
        foreach (var old in superseded.Where(old => old.Siege == siege && old.UntoldLosses > 0))
        {
            pending.UntoldLosses = Math.Max(pending.UntoldLosses, old.UntoldLosses);
            old.UntoldLosses = 0;
        }
        network.SendAll(pending.Request);
    }

    // The only writer of pending, so the Reject lock always matches it.
    private void SetPending(Pending attempt)
    {
        pending = attempt;
        breakOut.SetBreakInPending(attempt != null);
    }

    private void Supersede(Pending attempt)
    {
        if (attempt == null) return;
        if (superseded.Count >= MaxSupersededRequests) superseded.RemoveAt(0);
        superseded.Add(attempt);
    }

    private void HandleResult(MessagePayload<NetworkBreakInResult> payload)
    {
        var result = payload.What;
        GameThread.RunSafe(() =>
        {
            bool isPending = pending != null && pending.Request.RequestId == result.RequestId;
            var attempt = isPending ? pending : superseded.FirstOrDefault(old => old.Request.RequestId == result.RequestId);
            if (attempt == null) return;
            // Dropping the attempt on first use drops duplicate replies.
            if (isPending) SetPending(null);
            else superseded.Remove(attempt);
            if (!result.Approved)
            {
                Logger.Warning("Server rejected break in request {RequestId}", result.RequestId);
                if (attempt.UntoldLosses > 0) TellLosses(attempt, attempt.UntoldLosses);
                return;
            }
            // One paid receipt covers the siege, so older attempts for it have nothing left to tell.
            superseded.RemoveAll(old => old.Siege == attempt.Siege);
            if (!isPending || !attempt.IsCurrent())
            {
                // Protobuf sends an empty array as null, e.g. an army leader whose own party lost nobody.
                int lost = (result.Counts ?? Array.Empty<int>()).Sum() + Math.Max(0, result.ArmyCasualties);
                Logger.Warning("Break in request {RequestId} was paid after the siege encounter changed", result.RequestId);
                TellLosses(attempt, lost);
                return;
            }
            if (!casualtyDecoder.TryDecode(result.CharacterIds, result.Counts, out var casualties)) return;
            breakOut.ShowBreakInDebrief(casualties, result.ArmyCasualties);
        }, context: nameof(NetworkBreakInResult));
    }

    // The server already took the troops and the roster sync carries that, so say why nobody got in. A newer attempt
    // for the same siege gets the same paid receipt back, so it tells the player instead, or walks them in.
    private void TellLosses(Pending attempt, int lost)
    {
        if (pending != null && pending.Siege == attempt.Siege)
        {
            pending.UntoldLosses = Math.Max(pending.UntoldLosses, lost);
            return;
        }
        messageBroker.Publish(this, new SendInformationMessage(
            $"The siege changed before your break in went through. {lost} troops were already lost getting there."));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BreakInAttempted>(HandleAttempt);
        messageBroker.Unsubscribe<NetworkBreakInResult>(HandleResult);
        superseded.Clear();
        SetPending(null);
    }
}
