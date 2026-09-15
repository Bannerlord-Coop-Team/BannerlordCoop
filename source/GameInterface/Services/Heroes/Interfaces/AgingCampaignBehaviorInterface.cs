using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.CoopSessionData;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Cutscenes.Messages;
using GameInterface.Services.UI.Notifications.Messages;
using Helpers;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Heroes.Interfaces;

public interface IAgingCampaignBehaviorInterface : IGameAbstraction
{
    void DailyTickHero(AgingCampaignBehavior behavior, Hero hero);
    void OnHeroComesOfAge(AgingCampaignBehavior behavior, Hero hero);
    void OnPlayerClanHeroReachesTeenAge(AgingCampaignBehavior behavior, Hero hero);
    bool GetIsPlayerIll(Hero hero);
    int GetPlayerIllDays(Hero hero);
    void AddPlayerKeys(string playerHeroId);
}

public class AgingCampaignBehaviorInterface : IAgingCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgingCampaignBehaviorInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IMissionMembershipRegistry missionMembershipRegistry;

    private AgingPlayerData AgingPlayerData => coopSessionProvider.CoopSession.AgingPlayerData;

    public AgingCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        INetwork network,
        IPlayerManager playerManager,
        IMissionMembershipRegistry missionMembershipRegistry = null)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.network = network;
        this.playerManager = playerManager;
        this.missionMembershipRegistry = missionMembershipRegistry;
    }

    public void DailyTickHero(AgingCampaignBehavior behavior, Hero hero)
    {
        bool isGameStart = (int)CampaignTime.Now.ToDays == behavior._gameStartDay;
        if (CampaignOptions.IsLifeDeathCycleDisabled || isGameStart || hero.IsTemplate) return;

        if (hero.IsAlive && hero.CanDie(KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge))
        {
            if (hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None
                && CanApplyDeathMark(hero)
                && (hero.PartyBelongedTo == null || (hero.PartyBelongedTo.MapEvent == null && hero.PartyBelongedTo.SiegeEvent == null)))
            {
                KillCharacterAction.ApplyByDeathMark(hero, false);
            }
            else
            {
                IsItTimeOfDeath(behavior, hero);
            }
        }

        if (behavior._heroesYoungerThanHeroComesOfAge.TryGetValue(hero, out int recordedAge))
        {
            int heroAge = (int)hero.Age;
            if (recordedAge != heroAge)
            {
                if (heroAge >= Campaign.Current.Models.AgeModel.HeroComesOfAge)
                {
                    behavior._heroesYoungerThanHeroComesOfAge.Remove(hero);
                    CampaignEventDispatcher.Instance.OnHeroComesOfAge(hero);

                    if (hero.Clan?.IsPlayerClan() == true)
                    {
                        MessageBroker.Instance.Publish(this, new InitiateCutsceneHeroComesOfAge(hero));
                    }
                }
                else
                {
                    behavior._heroesYoungerThanHeroComesOfAge[hero] = heroAge;
                    if (heroAge == Campaign.Current.Models.AgeModel.BecomeTeenagerAge)
                    {
                        CampaignEventDispatcher.Instance.OnHeroReachesTeenAge(hero);
                    }
                    else if (heroAge == Campaign.Current.Models.AgeModel.BecomeChildAge)
                    {
                        CampaignEventDispatcher.Instance.OnHeroGrowsOutOfInfancy(hero);
                    }
                }
            }
        }
        if (hero.IsPlayerHero()
            && GetIsPlayerIll(hero)
            && hero.HeroState != Hero.CharacterStates.Dead
            && CanProcessNaturalDeath(hero))
        {
            AddPlayerIllDays(hero, 1);
            if (GetPlayerIllDays(hero) > 3)
            {
                hero.HitPoints -= MathF.Ceiling((float)hero.HitPoints * (0.05f * (float)GetPlayerIllDays(hero)));
                if (hero.HitPoints <= 1 && hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
                {
                    if (behavior._extraLivesContainer.TryGetValue(hero, out int numberOfExtraLives))
                    {
                        if (numberOfExtraLives == 0)
                        {
                            KillPlayerHeroWithIllness(hero);
                            return;
                        }
                        SetPlayerIllDays(hero, -1);
                        behavior._extraLivesContainer[hero] = numberOfExtraLives - 1;
                        if (behavior._extraLivesContainer[hero] == 0)
                        {
                            behavior._extraLivesContainer.Remove(hero);
                            return;
                        }
                    }
                    else
                    {
                        KillPlayerHeroWithIllness(hero);
                    }
                }
            }
        }
    }

    public void OnHeroComesOfAge(AgingCampaignBehavior behavior, Hero hero)
    {
        if (hero.HeroState != Hero.CharacterStates.Active)
        {
            // Replace Clan.PlayerClan usage
            if (!hero.Clan.IsPlayerClan())
            {
                foreach (ValueTuple<SkillObject, int> inheritedSkills in Campaign.Current.Models.HeroCreationModel.GetInheritedSkillsForHero(hero))
                {
                    hero.SetSkillValue(inheritedSkills.Item1, inheritedSkills.Item2);
                }
                hero.HeroDeveloper.InitializeHeroDeveloper();
            }
            else
            {
                hero.HeroDeveloper.SetInitialLevel(hero.Level);
            }

            Equipment battleEquipment = Campaign.Current.Models.EquipmentSelectionModel.GetEquipmentForHeroComeOfAge(hero, Equipment.EquipmentType.Battle);
            Equipment civilianEquipment = Campaign.Current.Models.EquipmentSelectionModel.GetEquipmentForHeroComeOfAge(hero, Equipment.EquipmentType.Civilian);

            battleEquipment ??= MBEquipmentRosterExtensions.All.Find(x => x.StringId == "generic_bat_dummy").GetBattleEquipments().First<Equipment>();
            civilianEquipment ??= MBEquipmentRosterExtensions.All.Find(x => x.StringId == "generic_civ_dummy").GetCivilianEquipments().First<Equipment>();

            EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, battleEquipment);
            EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, civilianEquipment);
        }
    }

    public void OnPlayerClanHeroReachesTeenAge(AgingCampaignBehavior behavior, Hero hero)
    {
        Equipment equipmentForHeroReachesTeenAge = Campaign.Current.Models.EquipmentSelectionModel.GetEquipmentForHeroReachesTeenAge(hero);
        if (equipmentForHeroReachesTeenAge != null)
        {
            EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, equipmentForHeroReachesTeenAge);
            new Equipment(Equipment.EquipmentType.Battle).FillFrom(equipmentForHeroReachesTeenAge, false);
            EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, equipmentForHeroReachesTeenAge);
        }
    }

    public bool GetIsPlayerIll(Hero hero)
    {
        return GetPlayerIllDays(hero) != -1;
    }

    public int GetPlayerIllDays(Hero hero)
    {
        if (!hero.IsPlayerHero()) return -1;

        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return -1;

        if (!AgingPlayerData.PlayerIsIllDays.ContainsKey(heroId)) return -1;

        return AgingPlayerData.PlayerIsIllDays[heroId];
    }

    private void AddPlayerIllDays(Hero hero, int daysToAdd)
    {
        if (!hero.IsPlayerHero()) return;

        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return;

        AgingPlayerData.PlayerIsIllDays[heroId] += daysToAdd;

        // Update for clients
        network.SendAll(new NetworkUpdatePlayerIllDays(heroId, AgingPlayerData.PlayerIsIllDays[heroId]));
    }

    private void SetPlayerIllDays(Hero hero, int newDaysIll)
    {
        if (!hero.IsPlayerHero()) return;

        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return;

        AgingPlayerData.PlayerIsIllDays[heroId] = newDaysIll;

        // Update for clients
        network.SendAll(new NetworkUpdatePlayerIllDays(heroId, AgingPlayerData.PlayerIsIllDays[heroId]));
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (AgingPlayerData == null)
        {
            Logger.Error("AgingPlayerData was null");
            return;
        }

        if (!AgingPlayerData.PlayerIsIllDays.ContainsKey(playerHeroId))
        {
            AgingPlayerData.PlayerIsIllDays[playerHeroId] = -1;
        }
    }

    private void IsItTimeOfDeath(AgingCampaignBehavior behavior, Hero hero)
    {
        if (!CanProcessNaturalDeath(hero)) return;

        if (hero.IsAlive
            && hero.Age >= (float)Campaign.Current.Models.AgeModel.BecomeOldAge
            && !CampaignOptions.IsLifeDeathCycleDisabled
            && hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None
            && MBRandom.RandomFloat < hero.ProbabilityOfDeath)
        {
            if (behavior._extraLivesContainer.TryGetValue(hero, out var numberOfExtraLives) && numberOfExtraLives > 0)
            {
                behavior._extraLivesContainer[hero] = numberOfExtraLives - 1;
                if (behavior._extraLivesContainer[hero] == 0)
                {
                    behavior._extraLivesContainer.Remove(hero);
                    return;
                }
            }
            else
            {
                if (hero.IsPlayerHero() && !GetIsPlayerIll(hero))
                {
                    AddPlayerIllDays(hero, 1);

                    messageBroker.Publish(this, new NotifyCaughtIllness(hero));
                    return;
                }
                if (!hero.IsPlayerHero() && (hero.PartyBelongedTo == null || (hero.PartyBelongedTo.MapEvent == null && hero.PartyBelongedTo.SiegeEvent == null)))
                {
                    KillCharacterAction.ApplyByOldAge(hero, true);
                }
            }
        }
    }

    private bool CanProcessNaturalDeath(Hero hero)
    {
        if (!PlayerManager.TryGetControlledObjectInfo(hero, out var controlledObject)) return true;

        // Don't process for disconnected players
        if (playerManager.IsOwnerOfHeroDisconnected(hero)) return false;

        // Don't process for players involved in a battle
        if (hero.PartyBelongedTo?.MapEvent != null || hero.PartyBelongedTo?.SiegeEvent != null)
        {
            return false;
        }

        // Don't process for players in a mission
        return missionMembershipRegistry?.IsControllerInMission(controlledObject.ObjectControllerId) != true;
    }

    private bool CanApplyDeathMark(Hero hero)
    {
        return hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge
            || CanProcessNaturalDeath(hero);
    }

    private void KillPlayerHeroWithIllness(Hero hero)
    {
        hero.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge);
        KillCharacterAction.ApplyByOldAge(hero, true);
    }
}
