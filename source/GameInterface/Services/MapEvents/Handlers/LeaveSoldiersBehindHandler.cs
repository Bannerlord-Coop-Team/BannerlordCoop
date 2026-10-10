using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Handlers;

internal class LeaveSoldiersBehindHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LeaveSoldiersBehindHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IEncounterGameMenuBehaviorInterface encounterGameMenuBehaviorInterface;

    public LeaveSoldiersBehindHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IEncounterGameMenuBehaviorInterface encounterGameMenuBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.encounterGameMenuBehaviorInterface = encounterGameMenuBehaviorInterface;

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
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetEncounterGameMenuBehavior(out var encounterGameMenuBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

            encounterGameMenuBehaviorInterface.LeftSoldiersBehindConsequence(encounterGameMenuBehavior, mainHero, mainParty);
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
