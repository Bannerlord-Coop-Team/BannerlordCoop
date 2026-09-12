using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Handlers;

public class HitRewardHandler : IHandler
{
    private const string UpgradedTroopsScoreboardRefreshChannel = "UpgradedTroopsScoreboardRefreshChannel";

    private static readonly ILogger Logger = LogManager.GetLogger<HitRewardHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISendCoalescer coalescer;

    public HitRewardHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISendCoalescer coalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.coalescer = coalescer;

        messageBroker.Subscribe<TrackTroopForUpgrades>(Handle_TrackTroopForUpgrades);
        messageBroker.Subscribe<NetworkTrackTroopForUpgrades>(Handle_NetworkTrackTroopForUpgrades);

        messageBroker.Subscribe<BattleHitReward>(Handle_BattleHitReward);
        messageBroker.Subscribe<NetworkBattleHitReward>(Handle_NetworkBattleHitReward);

        messageBroker.Subscribe<CheckUpgradeAfterAgentRemoved>(Handle_CheckUpgradeAfterAgentRemoved);
        messageBroker.Subscribe<NetworkCheckUpgradeAfterAgentRemoved>(Handle_NetworkCheckUpgradeAfterAgentRemoved);

        messageBroker.Subscribe<NetworkUpdateScoreboardAfterUpgrades>(Handle_NetworkUpdateScoreboardAfterUpgrades);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TrackTroopForUpgrades>(Handle_TrackTroopForUpgrades);
        messageBroker.Unsubscribe<NetworkTrackTroopForUpgrades>(Handle_NetworkTrackTroopForUpgrades);

        messageBroker.Unsubscribe<BattleHitReward>(Handle_BattleHitReward);
        messageBroker.Unsubscribe<NetworkBattleHitReward>(Handle_NetworkBattleHitReward);

        messageBroker.Unsubscribe<CheckUpgradeAfterAgentRemoved>(Handle_CheckUpgradeAfterAgentRemoved);
        messageBroker.Unsubscribe<NetworkCheckUpgradeAfterAgentRemoved>(Handle_NetworkCheckUpgradeAfterAgentRemoved);

        messageBroker.Unsubscribe<NetworkUpdateScoreboardAfterUpgrades>(Handle_NetworkUpdateScoreboardAfterUpgrades);
    }

    private void Handle_TrackTroopForUpgrades(MessagePayload<TrackTroopForUpgrades> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            network.SendAll(new NetworkTrackTroopForUpgrades(data.MapEventPartyId, data.CharacterId));
        });
    }

    private void Handle_NetworkTrackTroopForUpgrades(MessagePayload<NetworkTrackTroopForUpgrades> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEventParty>(data.MapEventPartyId, out var mapEventParty)) return;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.CharacterId, out var character)) return;

            mapEventParty.Party.MapEvent?.TroopUpgradeTracker.AddTrackedTroop(mapEventParty.Party, character);
        });
    }

    private void Handle_BattleHitReward(MessagePayload<BattleHitReward> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MapEvent, out var mapEventId)) return;
            if (!objectManager.TryGetIdWithLogging(data.AffectedCharacter, out var affectedCharacterId)) return;
            if (!objectManager.TryGetIdWithLogging(data.AffectorCharacter, out var affectorCharacterId)) return;

            string captainId = null;
            if (data.Captain != null && !objectManager.TryGetIdWithLogging(data.Captain, out captainId)) return;

            string heroId = null;
            if (data.Hero != null && !objectManager.TryGetIdWithLogging(data.Hero, out heroId)) return;

            if (!objectManager.TryGetIdWithLogging(data.AffectorParty, out var affectorPartyId)) return;

            var message = new NetworkBattleHitReward(
                mapEventId,
                affectedCharacterId,
                affectorCharacterId,
                captainId,
                heroId,
                data.AffectedAgentSide,
                data.AffectorAgentSide,
                data.IsAgentMounted,
                data.LastSpeedBonus,
                data.LastShotDifficulty,
                data.IsSiegeEngineHit,
                data.LastAttackerWeapon,
                data.AttackType,
                data.HitpointRatio,
                data.DamageAmount,
                affectorPartyId,
                data.IsSneakAttack,
                data.AffectedAgentHealth,
                data.IsAffectorUnderCommand);

            network.SendAll(message);
        });
    }

    private void Handle_NetworkBattleHitReward(MessagePayload<NetworkBattleHitReward> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;

            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.AffectedCharacterId, out var affectedCharacter)) return;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.AffectorCharacterId, out var affectorCharacter)) return;

            Hero captain = null;
            if (data.CaptainId != null && !objectManager.TryGetObjectWithLogging<Hero>(data.CaptainId, out captain)) return;

            Hero hero = null;
            if (data.HeroId != null && !objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out hero)) return;

            if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.AffectorPartyId, out var affectorParty)) return;

            bool isTeamKill = data.AffectedAgentSide == data.AffectorAgentSide;
            bool isHorseCharge = data.IsAgentMounted && data.AttackType == AgentAttackType.Collision;

            SkillLevelingManager.OnCombatHit(
                affectorCharacter,
                affectedCharacter,
                captain?.CharacterObject,
                hero,
                data.LastSpeedBonus,
                data.LastShotDifficulty,
                data.LastAttackerWeapon,
                data.HitpointRatio,
                CombatXpModel.MissionTypeEnum.Battle,
                data.IsAgentMounted,
                isTeamKill,
                data.IsAffectorUnderCommand,
                data.DamageAmount,
                data.AffectedAgentHealth < 1f,
                data.IsSiegeEngineHit,
                isHorseCharge,
                data.IsSneakAttack);

            int upgradedCount = 0;

            // Applies changes to the TroopUpgradeTracker
            upgradedCount = mapEvent.TroopUpgradeTracker.CheckUpgradedCount(affectorParty, affectorCharacter);

            // Update scoreboard for clients
            var key = new CoalesceKey(UpgradedTroopsScoreboardRefreshChannel, data.AffectorPartyId + data.AffectorCharacterId);
            coalescer.Enqueue(key, new LatestWinsPayload(new NetworkUpdateScoreboardAfterUpgrades(data.MapEventId, data.AffectorCharacterId, data.AffectorPartyId, data.AffectorAgentSide, upgradedCount)));
        });
    }

    private void Handle_CheckUpgradeAfterAgentRemoved(MessagePayload<CheckUpgradeAfterAgentRemoved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MapEvent, out var mapEventId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Party, out var partyId)) return;
            if (!objectManager.TryGetIdWithLogging(data.CharacterObject, out var characterObjectId)) return;

            var message = new NetworkCheckUpgradeAfterAgentRemoved(
                mapEventId,
                partyId,
                characterObjectId,
                data.Side);

            network.SendAll(message);
        });
    }

    private void Handle_NetworkCheckUpgradeAfterAgentRemoved(MessagePayload<NetworkCheckUpgradeAfterAgentRemoved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.PartyId, out var party)) return;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.CharacterObjectId, out var character)) return;

            // Applies changes to the TroopUpgradeTracker
            var upgradedCount = mapEvent.TroopUpgradeTracker.CheckUpgradedCount(party, character);

            // Update scoreboard for clients
            var key = new CoalesceKey(UpgradedTroopsScoreboardRefreshChannel, data.PartyId + data.CharacterObjectId);
            coalescer.Enqueue(key, new LatestWinsPayload(new NetworkUpdateScoreboardAfterUpgrades(data.MapEventId, data.CharacterObjectId, data.PartyId, data.Side, upgradedCount)));
        });
    }

    private void Handle_NetworkUpdateScoreboardAfterUpgrades(MessagePayload<NetworkUpdateScoreboardAfterUpgrades> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            var mission = Mission.Current;
            if (mission == null) return;

            if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.AffectorCharacterId, out var affectorCharacter)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.AffectorPartyId, out var affectorParty)) return;

            // Skip update if the client is not in this map event
            if (MapEvent.PlayerMapEvent != mapEvent) return;

            BattleObserverMissionLogic battleObserverMissionLogic = mission.GetMissionBehavior<BattleObserverMissionLogic>();
            if ((battleObserverMissionLogic?.BattleObserver) == null) return;

            TroopUpgradeTracker troopUpgradeTracker = mapEvent.TroopUpgradeTracker;
            if (affectorCharacter.IsHero)
            {
                Hero heroObject = affectorCharacter.HeroObject;
                using (IEnumerator<SkillObject> enumerator = troopUpgradeTracker.CheckSkillUpgrades(heroObject).GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        SkillObject skill = enumerator.Current;
                        battleObserverMissionLogic.BattleObserver.HeroSkillIncreased(data.AffectorAgentSide, affectorParty, affectorCharacter, skill);
                    }
                    return;
                }
            }

            if (data.UpgradedCount != 0)
            {
                battleObserverMissionLogic.BattleObserver.TroopNumberChanged(data.AffectorAgentSide, affectorParty, affectorCharacter, 0, 0, 0, 0, 0, data.UpgradedCount);
            }
        });
    }
}
