using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Actions.Handlers;

internal class BribeGuardsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BribeGuardsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionTradePlayerDataInterface sessionTradePlayerDataInterface;

    public BribeGuardsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionTradePlayerDataInterface sessionTradePlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionTradePlayerDataInterface = sessionTradePlayerDataInterface;

        messageBroker.Subscribe<PlayerBribesGuard>(Handle_PlayerBribesGuard);
        messageBroker.Subscribe<NetworkPlayerBribesGuard>(Handle_NetworkPlayerBribesGuard);
        messageBroker.Subscribe<NetworkPlayerBribesGuardClient>(Handle_NetworkPlayerBribesGuardClient);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerBribesGuard>(Handle_PlayerBribesGuard);
        messageBroker.Unsubscribe<NetworkPlayerBribesGuard>(Handle_NetworkPlayerBribesGuard);
        messageBroker.Unsubscribe<NetworkPlayerBribesGuardClient>(Handle_NetworkPlayerBribesGuardClient);
    }

    private void Handle_PlayerBribesGuard(MessagePayload<PlayerBribesGuard> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Settlement, out var settlementId)) return;

        var message = new NetworkPlayerBribesGuard(mainHeroId, settlementId, data.Gold);
        network.SendAll(message);
    }

    private void Handle_NetworkPlayerBribesGuard(MessagePayload<NetworkPlayerBribesGuard> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            if (mainHero.Gold - data.Gold < 0)
            {
                Logger.Error($"Rejecting player bribe due to a lack of gold. Requested change: {data.Gold} Player gold: {mainHero.Gold}");
                return;
            }

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, data.Gold, false);

            sessionTradePlayerDataInterface.UpdateSettlementBribePaid(data.MainHeroId, data.SettlementId, data.Gold);

            // Update last to ensure gold action and session are updated.
            // Skill xp is least important here if playerParty is null.
            if (MBRandom.RandomFloat < (float)data.Gold / 1000f)
            {
                var playerParty = mainHero.PartyBelongedTo;
                if (playerParty == null) return;

                float skillXp = (float)data.Gold * 0.1f;
                DefaultSkillLevelingManager.OnPartySkillExercised(mainHero.PartyBelongedTo, DefaultSkills.Roguery, skillXp, PartyRole.PartyLeader);
            }

            network.Send(obj.Who as NetPeer, new NetworkPlayerBribesGuardClient(data.SettlementId, data.Gold));
        });
    }

    private void Handle_NetworkPlayerBribesGuardClient(MessagePayload<NetworkPlayerBribesGuardClient> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            // Locally set the BribePaid on the settlement, this is unique per client
            settlement.BribePaid += data.Gold;
        });
    }
}