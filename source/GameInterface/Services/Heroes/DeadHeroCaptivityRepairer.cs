using Common.Logging;
using Common.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Heroes;

internal interface IDeadHeroCaptivityRepairer
{
    bool TryRestoreDeadState(Hero hero);
    int RepairLoadedState(CampaignObjectManager objectManager);
}

internal class DeadHeroCaptivityRepairer : IDeadHeroCaptivityRepairer
{
    private static readonly ILogger Logger = LogManager.GetLogger<DeadHeroCaptivityRepairer>();

    public bool TryRestoreDeadState(Hero hero)
    {
        if (hero?.HeroState != Hero.CharacterStates.Prisoner || !HasCompletedDeathFingerprint(hero))
            return false;

        // ChangeState reads progression owners that Hero.OnDeath deliberately released.
        hero._heroState = Hero.CharacterStates.Dead;
        Logger.Warning("Restored dead state for death-marked prisoner hero {HeroId}", hero.StringId);
        return true;
    }

    public int RepairLoadedState(CampaignObjectManager objectManager)
    {
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));

        int repairedCount = 0;
        var rostersToRebuild = new HashSet<TroopRoster>();
        var heroes = objectManager.DeadOrDisabledHeroes.Concat(objectManager.AliveHeroes).ToList();
        foreach (var hero in heroes)
        {
            bool repaired = ReconcileHeroCollection(objectManager, hero);
            repaired |= TryRepairCaptivityRoster(hero, rostersToRebuild);
            if (repaired)
                repairedCount++;
        }

        RebuildCaptivityRosters(rostersToRebuild);

        if (repairedCount > 0)
            Logger.Warning("Repaired loaded captivity state for {Count} completed-death heroes", repairedCount);

        return repairedCount;
    }

    private static bool ReconcileHeroCollection(CampaignObjectManager objectManager, Hero hero)
    {
        if (hero?.HeroState != Hero.CharacterStates.Dead ||
            !HasCompletedDeathFingerprint(hero) ||
            !objectManager._aliveHeroes.Remove(hero))
            return false;

        if (!objectManager._deadOrDisabledHeroes.Contains(hero))
            objectManager._deadOrDisabledHeroes.Add(hero);

        return true;
    }

    private static bool TryRepairCaptivityRoster(Hero hero, ISet<TroopRoster> rostersToRebuild)
    {
        if (hero?.HeroState != Hero.CharacterStates.Dead ||
            !HasCompletedDeathFingerprint(hero) ||
            hero.PartyBelongedToAsPrisoner == null)
            return false;

        var captorRoster = hero.PartyBelongedToAsPrisoner.PrisonRoster;

        // Every peer repairs the same loaded save, so roster sync patches must not publish load-time deltas.
        using (new AllowedThread())
        {
            if (captorRoster != null)
            {
                int heroIndex = hero.CharacterObject == null
                    ? -1
                    : captorRoster.FindIndexOfTroop(hero.CharacterObject);
                if (heroIndex >= 0)
                {
                    captorRoster.SetElementWoundedNumber(heroIndex, 0);
                    captorRoster.SetElementNumber(heroIndex, 0);
                }

                rostersToRebuild.Add(captorRoster);
            }

            hero.PartyBelongedToAsPrisoner = null;
        }

        return true;
    }

    private static void RebuildCaptivityRosters(IEnumerable<TroopRoster> rosters)
    {
        using (new AllowedThread())
        {
            foreach (var roster in rosters)
            {
                roster.RemoveZeroCounts();
                roster.InitializeCachedData();
            }
        }
    }

    private static bool HasCompletedDeathFingerprint(Hero hero) =>
        hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None &&
        hero._deathDay != CampaignTime.Never &&
        hero._heroSkills == null &&
        hero._heroPerks == null &&
        hero._heroTraits == null &&
        hero._characterAttributes == null &&
        hero._heroDeveloper == null &&
        hero.VolunteerTypes == null;
}
