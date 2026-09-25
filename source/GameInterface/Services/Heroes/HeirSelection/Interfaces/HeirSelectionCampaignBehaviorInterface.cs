using GameInterface.CoopSessionData;
using GameInterface.Services.Clans;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.HeirSelection.Interfaces;

public interface IHeirSelectionCampaignBehaviorInterface : IGameAbstraction
{
    void OnBeforePlayerCharacterChanged(Hero oldPlayerHero, MobileParty originalParty);
    void OnPlayerCharacterChanged(Hero oldPlayerHero, Hero newPlayerHero, MobileParty newPlayerParty, bool isPartyChanged);
    PlayerSuccessionData GetSuccession(Hero hero);
    void PrepareSuccession(Hero hero);
    Dictionary<Hero, int> GetHeirs(Hero hero);
    Dictionary<Hero, int> GetPlayerSuccessors(Hero hero);
    bool IsWaitingForLeader(Hero hero);
}

public class HeirSelectionCampaignBehaviorInterface : IHeirSelectionCampaignBehaviorInterface
{
    private readonly Dictionary<Hero, ItemRoster> playerHeroItemsThatWillBeInherited;

    private readonly Dictionary<Hero, ItemRoster> playerHeroEquipmentsThatWillBeInherited;

    private readonly ICoopSessionProvider sessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IClanMemberGrouping grouping;

    private Dictionary<string, PlayerSuccessionData> Successions => sessionProvider.CoopSession.AgingPlayerData.PlayerSuccessions;

    public HeirSelectionCampaignBehaviorInterface(ICoopSessionProvider sessionProvider, IObjectManager objectManager,
        IPlayerManager playerManager, IClanMemberGrouping grouping)
    {
        this.sessionProvider = sessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.grouping = grouping;
        playerHeroItemsThatWillBeInherited = new();
        playerHeroEquipmentsThatWillBeInherited = new();
    }

    public PlayerSuccessionData GetSuccession(Hero hero)
    {
        return hero != null && objectManager.TryGetId(hero, out var id) && Successions.TryGetValue(id, out var data)
            ? data : null;
    }

    public void PrepareSuccession(Hero hero)
    {
        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return;
        if (!Successions.TryGetValue(heroId, out var data))
            Successions[heroId] = data = new PlayerSuccessionData();

        if (data.DeathRecorded) return;
        data.DeathRecorded = true;
        data.WasClanLeader = hero.Clan?.Leader == hero;

        // Death clears Spouse. Keep both original family roots until each player has inherited.
        if (hero.Spouse == null || !objectManager.TryGetIdWithLogging(hero.Spouse, out var spouseId)) return;
        data.SpouseId = spouseId;
        if (!playerManager.Contains(hero.Spouse)) return;
        if (!Successions.TryGetValue(spouseId, out var spouseData))
            Successions[spouseId] = spouseData = new PlayerSuccessionData();
        spouseData.SpouseId = heroId;
    }

    public Dictionary<Hero, int> GetHeirs(Hero hero)
    {
        return hero.Clan?.GetHeirApparents().Where(pair => CanInherit(hero, pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value) ?? new();
    }

    internal bool CanInherit(Hero hero, Hero candidate)
    {
        if (candidate == hero || playerManager.Contains(candidate)) return false;
        if (grouping.AreRelated(candidate, hero)) return true;

        var spouseId = GetSuccession(hero)?.SpouseId;
        return spouseId != null && objectManager.TryGetObject<Hero>(spouseId, out var spouse) &&
            grouping.AreRelated(candidate, spouse);
    }

    public Dictionary<Hero, int> GetPlayerSuccessors(Hero hero)
    {
        if (hero.Clan?.Leader != hero) return new();
        return hero.Clan.Heroes.Where(candidate => candidate != hero && playerManager.Contains(candidate) &&
                candidate.IsAlive && !candidate.IsChild && !candidate.IsDisabled && !candidate.IsNotSpawned &&
                candidate.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
            .ToDictionary(candidate => candidate, candidate =>
                candidate.GetSkillValue(DefaultSkills.Charm) + candidate.GetSkillValue(DefaultSkills.Leadership));
    }

    public bool IsWaitingForLeader(Hero hero)
    {
        var leader = hero.Clan?.Leader;
        return leader != null && leader != hero && leader.IsDead &&
            GetSuccession(leader) is { WasClanLeader: true, GameOver: false } &&
            objectManager.TryGetId(leader, out var leaderId) && GetSuccession(hero)?.SpouseId == leaderId &&
            GetHeirs(leader).Count > 0;
    }

    public void OnBeforePlayerCharacterChanged(Hero oldPlayerHero, MobileParty originalParty)
    {
        playerHeroItemsThatWillBeInherited[oldPlayerHero] = new();
        playerHeroEquipmentsThatWillBeInherited[oldPlayerHero] = new();

        if (originalParty != null)
        {
            foreach (ItemRosterElement itemRosterElement in originalParty.ItemRoster)
            {
                playerHeroItemsThatWillBeInherited[oldPlayerHero].Add(itemRosterElement);
            }
        }
        for (int i = 0; i < 12; i++)
        {
            if (!oldPlayerHero.BattleEquipment[i].IsEmpty)
            {
                playerHeroEquipmentsThatWillBeInherited[oldPlayerHero].AddToCounts(oldPlayerHero.BattleEquipment[i], 1);
            }
            if (!oldPlayerHero.CivilianEquipment[i].IsEmpty)
            {
                playerHeroEquipmentsThatWillBeInherited[oldPlayerHero].AddToCounts(oldPlayerHero.CivilianEquipment[i], 1);
            }
        }
    }

    public void OnPlayerCharacterChanged(Hero oldPlayerHero, Hero newPlayerHero, MobileParty newPlayerParty, bool isPartyChanged)
    {
        if (!playerHeroItemsThatWillBeInherited.ContainsKey(oldPlayerHero)) return;
        if (!playerHeroEquipmentsThatWillBeInherited.ContainsKey(oldPlayerHero)) return;

        if (isPartyChanged)
        {
            newPlayerParty.ItemRoster.Add(playerHeroItemsThatWillBeInherited[oldPlayerHero]);
        }
        newPlayerParty.ItemRoster.Add(playerHeroEquipmentsThatWillBeInherited[oldPlayerHero]);

        playerHeroItemsThatWillBeInherited.Remove(oldPlayerHero);
        playerHeroEquipmentsThatWillBeInherited.Remove(oldPlayerHero);
    }
}
