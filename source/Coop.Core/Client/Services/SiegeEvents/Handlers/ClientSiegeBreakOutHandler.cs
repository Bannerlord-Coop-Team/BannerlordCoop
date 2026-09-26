using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Coop.Core.Client.Services.SiegeEvents.Handlers;

internal class ClientSiegeBreakOutHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientSiegeBreakOutHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISiegeBreakOut breakOut;
    private Pending pending;

    private sealed class Pending
    {
        public NetworkRequestBreakOut Request;
        public MobileParty Party;
        public Settlement Settlement;
        public SiegeEvent Siege;
        public PlayerEncounter Encounter;
        public string MenuId;

        public bool IsCurrent() => MobileParty.MainParty == Party && Party.CurrentSettlement == Settlement &&
            Settlement.SiegeEvent == Siege && ReferenceEquals(PlayerEncounter.Current, Encounter) &&
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId == MenuId;
    }

    public ClientSiegeBreakOutHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, ISiegeBreakOut breakOut)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.breakOut = breakOut;
        messageBroker.Subscribe<BreakOutAttempted>(HandleAttempt);
        messageBroker.Subscribe<NetworkBreakOutResult>(HandleResult);
    }

    private void HandleAttempt(MessagePayload<BreakOutAttempted> payload)
    {
        if (pending != null && pending.IsCurrent())
        {
            network.SendAll(pending.Request);
            return;
        }
        pending = null;
        var party = payload.What.Party;
        var settlement = party?.CurrentSettlement;
        var siege = settlement?.SiegeEvent;
        if (party != MobileParty.MainParty || siege == null || PlayerEncounter.Current == null) return;
        if (!objectManager.TryGetHandleWithLogging(party, out var partyId) ||
            !objectManager.TryGetHandleWithLogging(settlement, out var settlementId) ||
            !objectManager.TryGetHandleWithLogging(siege, out var siegeId)) return;

        pending = new Pending
        {
            Request = new NetworkRequestBreakOut(Guid.NewGuid().ToString(), partyId, settlementId, siegeId),
            Party = party,
            Settlement = settlement,
            Siege = siege,
            Encounter = PlayerEncounter.Current,
            MenuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId,
        };
        network.SendAll(pending.Request);
    }

    private void HandleResult(MessagePayload<NetworkBreakOutResult> payload)
    {
        var result = payload.What;
        GameThread.RunSafe(() =>
        {
            if (pending == null || pending.Request.RequestId != result.RequestId) return;
            var attempt = pending;
            pending = null;
            if (!attempt.IsCurrent()) return;
            if (!result.Approved)
            {
                Logger.Warning("Server rejected breakout request {RequestId}", result.RequestId);
                return;
            }
            if (result.CharacterIds == null || result.Counts == null || result.CharacterIds.Length != result.Counts.Length) return;
            var casualties = TroopRoster.CreateDummyTroopRoster();
            for (int index = 0; index < result.CharacterIds.Length; index++)
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(result.CharacterIds[index], out var character)) return;
                casualties.AddToCounts(character, result.Counts[index]);
            }
            breakOut.ShowDebrief(casualties, result.ArmyCasualties);
        }, context: nameof(NetworkBreakOutResult));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BreakOutAttempted>(HandleAttempt);
        messageBroker.Unsubscribe<NetworkBreakOutResult>(HandleResult);
        pending = null;
    }
}
