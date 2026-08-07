using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.BattleRetreat.Messages;
using Coop.Core.Server.Services.BattleRetreat.Messages;
using GameInterface.Services.MapEvents.Messages.Retreat;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Client.Services.BattleRetreat.Handlers;

/// <summary>
/// Forwards a retreat intent to the server and applies the verdict it broadcasts.
/// </summary>
internal class ClientBattleRetreatHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientBattleRetreatHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ClientBattleRetreatHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<BattleRetreatAttempted>(HandleAttempt);
        messageBroker.Subscribe<BattleMissionRetreatAttempted>(HandleMissionRetreat);
        messageBroker.Subscribe<NetworkBattleRetreatResolved>(HandleResolved);
        messageBroker.Subscribe<BreakInCasualtiesAttempted>(HandleBreakInCasualties);
    }

    /// <summary>The local battle mission ended without resolving; ask the server to leave the battle.</summary>
    private void HandleMissionRetreat(MessagePayload<BattleMissionRetreatAttempted> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Battle, out var mapEventId)) return;

        network.SendAll(new NetworkRequestBattleMissionRetreat(partyId, mapEventId));
    }

    private void HandleAttempt(MessagePayload<BattleRetreatAttempted> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Battle, out var mapEventId)) return;

        network.SendAll(new NetworkRequestBattleRetreat(partyId, mapEventId));
    }

    /// <summary>
    /// The server's verdict on a retreat. Two unrelated clients care about it: the one that asked, and any
    /// whose siege camp the retreat dissolved.
    /// </summary>
    private void HandleResolved(MessagePayload<NetworkBattleRetreatResolved> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObject<MobileParty>(obj.PartyId, out var party)) return;

            if (party.IsControlledByThisInstance())
                ApplyOwnRetreatVerdict(obj);
            else
                LeaveSiegeIfOurCampWasCleared(obj);
        });
    }

    /// <summary>[Game thread] This client asked for the retreat: leave the encounter, or stay put if refused.</summary>
    private static void ApplyOwnRetreatVerdict(NetworkBattleRetreatResolved obj)
    {
        if (!obj.Approved)
        {
            Logger.Information("Server refused the retreat; staying in the encounter");
            return;
        }

        using (new AllowedThread())
        {
            if (PlayerEncounter.Current != null) PlayerEncounter.Finish(true);
            else GameMenu.ExitToLast();
        }
    }

    /// <summary>[Game thread] Someone else's retreat dissolved a siege camp this client was part of.</summary>
    private void LeaveSiegeIfOurCampWasCleared(NetworkBattleRetreatResolved obj)
    {
        if (!obj.Approved || obj.CampClearedPartyIds == null) return;

        var mine = MobileParty.MainParty;
        if (mine == null || !objectManager.TryGetId(mine, out var myId)) return;
        if (!obj.CampClearedPartyIds.Contains(myId)) return;

        using (new AllowedThread())
        {
            if (PlayerSiege.PlayerSiegeEvent != null) PlayerSiege.FinalizePlayerSiege();
        }
    }

    private void HandleBreakInCasualties(MessagePayload<BreakInCasualtiesAttempted> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkRequestBreakInCasualties(partyId, settlementId));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleRetreatAttempted>(HandleAttempt);
        messageBroker.Unsubscribe<BattleMissionRetreatAttempted>(HandleMissionRetreat);
        messageBroker.Unsubscribe<NetworkBattleRetreatResolved>(HandleResolved);
        messageBroker.Unsubscribe<BreakInCasualtiesAttempted>(HandleBreakInCasualties);
    }
}
