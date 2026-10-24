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
    // Requests replaced before their reply came, so a late paid reply still tells the player what it cost.
    private readonly List<string> supersededRequestIds = new List<string>();

    private sealed class Pending
    {
        public NetworkRequestBreakIn Request;
        public MobileParty Party;
        public Settlement Settlement;
        public SiegeEvent Siege;
        public PlayerEncounter Encounter;
        public string MenuId;

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
        if (supersededRequestIds.Count >= MaxSupersededRequests) supersededRequestIds.RemoveAt(0);
        supersededRequestIds.Add(attempt.Request.RequestId);
    }

    private void HandleResult(MessagePayload<NetworkBreakInResult> payload)
    {
        var result = payload.What;
        GameThread.RunSafe(() =>
        {
            // Removing the id on first use drops duplicate replies.
            bool superseded = supersededRequestIds.Remove(result.RequestId);
            bool isPending = pending != null && pending.Request.RequestId == result.RequestId;
            if (!superseded && !isPending) return;
            var attempt = isPending ? pending : null;
            if (isPending) SetPending(null);
            if (!result.Approved)
            {
                Logger.Warning("Server rejected break in request {RequestId}", result.RequestId);
                return;
            }
            if (attempt == null || !attempt.IsCurrent())
            {
                // The server already took the troops and the roster sync carries that, so say why nobody got in.
                // Protobuf sends an empty array as null, e.g. an army leader whose own party lost nobody.
                int lost = (result.Counts ?? Array.Empty<int>()).Sum() + Math.Max(0, result.ArmyCasualties);
                Logger.Warning("Break in request {RequestId} was paid after the siege encounter changed", result.RequestId);
                messageBroker.Publish(this, new SendInformationMessage(
                    $"The siege changed before your break in went through. {lost} troops were already lost getting there."));
                return;
            }
            if (!casualtyDecoder.TryDecode(result.CharacterIds, result.Counts, out var casualties)) return;
            breakOut.ShowBreakInDebrief(casualties, result.ArmyCasualties);
        }, context: nameof(NetworkBreakInResult));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BreakInAttempted>(HandleAttempt);
        messageBroker.Unsubscribe<NetworkBreakInResult>(HandleResult);
        supersededRequestIds.Clear();
        SetPending(null);
    }
}
