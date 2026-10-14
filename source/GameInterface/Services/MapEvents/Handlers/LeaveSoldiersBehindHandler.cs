using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

internal class LeaveSoldiersBehindHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LeaveSoldiersBehindHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IEncounterGameMenuBehaviorInterface encounterGameMenuBehaviorInterface;
    private readonly IPlayerManager playerManager;

    private readonly ConditionalWeakTable<MobileParty, MapEvent> appliedMapEvents = new();

    public LeaveSoldiersBehindHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IEncounterGameMenuBehaviorInterface encounterGameMenuBehaviorInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.encounterGameMenuBehaviorInterface = encounterGameMenuBehaviorInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<PlayerLeftSoldiersBehind>(Handle_PlayerLeftSoldiersBehind);
        messageBroker.Subscribe<NetworkPlayerLeftSoldiersBehind>(Handle_NetworkPlayerLeftSoldiersBehind);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerLeftSoldiersBehind>(Handle_PlayerLeftSoldiersBehind);
        messageBroker.Unsubscribe<NetworkPlayerLeftSoldiersBehind>(Handle_NetworkPlayerLeftSoldiersBehind);
    }

    private void Handle_PlayerLeftSoldiersBehind(MessagePayload<PlayerLeftSoldiersBehind> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;

        network.SendAll(new NetworkPlayerLeftSoldiersBehind(mainHeroId, mainPartyId));
    }

    private void Handle_NetworkPlayerLeftSoldiersBehind(MessagePayload<NetworkPlayerLeftSoldiersBehind> obj)
    {
        if (ModInformation.IsClient) return;

        var data = obj.What;

        if (obj.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player) ||
            player.HeroId != data.MainHeroId || player.MobilePartyId != data.MainPartyId)
        {
            Logger.Warning("Ignoring leave soldiers behind for party {PartyId} the sender does not control", data.MainPartyId);
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!TryGetEncounterGameMenuBehavior(out var encounterGameMenuBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

            var mapEvent = mainParty.MapEvent;
            if (mapEvent == null || mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None)
            {
                Logger.Warning("Ignoring leave soldiers behind: party {Party} is not in an unresolved battle", mainParty.StringId);
                return;
            }

            // A repeated request for the same map event (double click, resent message) must not sacrifice twice.
            if (appliedMapEvents.TryGetValue(mainParty, out var appliedMapEvent) && appliedMapEvent == mapEvent)
            {
                Logger.Warning("Ignoring repeated leave soldiers behind for {Party}", mainParty.StringId);
                return;
            }

            encounterGameMenuBehaviorInterface.LeftSoldiersBehindConsequence(encounterGameMenuBehavior, mainHero, mainParty);

            appliedMapEvents.Remove(mainParty);
            appliedMapEvents.Add(mainParty, mapEvent);
        });
    }

    private bool TryGetEncounterGameMenuBehavior(out EncounterGameMenuBehavior encounterGameMenuBehavior)
    {
        encounterGameMenuBehavior = Campaign.Current?.GetCampaignBehavior<EncounterGameMenuBehavior>();
        if (encounterGameMenuBehavior != null) return true;

        Logger.Debug("Skipping encounter game menu update because the campaign behavior is unavailable.");
        return false;
    }
}
