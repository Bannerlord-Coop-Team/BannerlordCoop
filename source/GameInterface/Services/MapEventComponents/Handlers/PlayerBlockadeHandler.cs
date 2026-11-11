using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventComponents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventComponents.Handlers;

/// <summary>
/// Asks the player leading an attacked blockade to defend or lift it, and lifts it on the server when they choose to.
/// </summary>
internal class PlayerBlockadeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerBlockadeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public PlayerBlockadeHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PlayerBlockadeAttacked>(Handle_PlayerBlockadeAttacked);
        messageBroker.Subscribe<NetworkPromptPlayerBlockadeAttacked>(Handle_NetworkPromptPlayerBlockadeAttacked);

        messageBroker.Subscribe<LiftPlayerBlockadeAttempted>(Handle_LiftPlayerBlockadeAttempted);
        messageBroker.Subscribe<NetworkRequestLiftPlayerBlockade>(Handle_NetworkRequestLiftPlayerBlockade);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerBlockadeAttacked>(Handle_PlayerBlockadeAttacked);
        messageBroker.Unsubscribe<NetworkPromptPlayerBlockadeAttacked>(Handle_NetworkPromptPlayerBlockadeAttacked);

        messageBroker.Unsubscribe<LiftPlayerBlockadeAttempted>(Handle_LiftPlayerBlockadeAttempted);
        messageBroker.Unsubscribe<NetworkRequestLiftPlayerBlockade>(Handle_NetworkRequestLiftPlayerBlockade);
    }

    private void Handle_PlayerBlockadeAttacked(MessagePayload<PlayerBlockadeAttacked> obj)
    {
        var mapEvent = obj.What.MapEvent;

        GameThread.EnqueueSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId)) return;

            network.SendAll(new NetworkPromptPlayerBlockadeAttacked(mapEventId));
        }, context: nameof(PlayerBlockadeAttacked));
    }

    private void Handle_NetworkPromptPlayerBlockadeAttacked(MessagePayload<NetworkPromptPlayerBlockadeAttacked> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;
            if (mapEvent.DefenderSide.LeaderParty != PartyBase.MainParty) return;

            // Same as BlockadeBattleMapEvent.OnInitialize for the main party
            using (new AllowedThread())
            {
                if (Campaign.Current.CurrentMenuContext == null)
                    GameMenu.ActivateGameMenu("player_blockade_got_attacked");
                else
                    GameMenu.SwitchToMenu("player_blockade_got_attacked");
            }
        }, context: nameof(NetworkPromptPlayerBlockadeAttacked));
    }

    private void Handle_LiftPlayerBlockadeAttempted(MessagePayload<LiftPlayerBlockadeAttempted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MapEvent, out var mapEventId)) return;

        network.SendAll(new NetworkRequestLiftPlayerBlockade(mapEventId));
    }

    private void Handle_NetworkRequestLiftPlayerBlockade(MessagePayload<NetworkRequestLiftPlayerBlockade> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObject<MapEvent>(data.MapEventId, out var mapEvent)) return;

            var siegeEvent = mapEvent.DefenderSide.LeaderParty.SiegeEvent;
            if ((!mapEvent.IsBlockade && !mapEvent.IsBlockadeSallyOut) || siegeEvent == null)
            {
                Logger.Warning("Ignoring the blockade lift for {MapEventId} because it is not a battle against a blockade", data.MapEventId);
                return;
            }

            // Same as EncounterGameMenuBehavior.lift_players_blockade
            var settlement = siegeEvent.BesiegedSettlement;
            var attackerParties = mapEvent.AttackerSide.Parties.ToList();
            siegeEvent.DeactivateBlockade();
            mapEvent.FinalizeEvent();

            // Replace PartyBase.MainParty usage, player parties sail in on their own
            foreach (var attacker in attackerParties)
            {
                var party = attacker.Party.MobileParty;
                if (party == null || party.IsPlayerParty() || party.CurrentSettlement != null || party.AttachedTo != null) continue;

                party.SetMoveGoToSettlement(settlement, MobileParty.NavigationType.Naval, isTargetingThePort: true);
            }
        }, context: nameof(NetworkRequestLiftPlayerBlockade));
    }
}
