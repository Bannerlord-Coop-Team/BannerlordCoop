using Common;
using GameInterface.Policies;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes;

[HarmonyPatch(typeof(HeroSpawnCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.HARMONY_CONFIGURED_MINOR_FACTION_CATEGORY)]
public static class ConfiguredMinorFactionHeroSpawner
{
    internal const string CoopTeamClanId = "coop_team";
    internal const string CoopTeamLeaderTemplateId = "coop_team_lord_another_joke";
    internal const string ConfiguredLordPrefix = "coop_team_lord_";

    [HarmonyPatch(
        nameof(HeroSpawnCampaignBehavior.SpawnMinorFactionHeroes),
        new[] { typeof(Clan), typeof(bool) })]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    internal static bool SpawnMinorFactionHeroesPrefix(Clan clan, bool firstTime)
    {
        bool hasAuthority = ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed();
        if (!ShouldReplaceNativeSpawn(clan?.StringId, hasAuthority)) return true;

        if (ShouldCreateInitialHeroes(clan.StringId, firstTime, hasAuthority))
            CreateInitialHeroes(clan);

        return false;
    }

    internal static bool ShouldReplaceNativeSpawn(string clanId, bool hasAuthority)
        => hasAuthority && string.Equals(clanId, CoopTeamClanId, StringComparison.Ordinal);

    internal static bool ShouldCreateInitialHeroes(string clanId, bool firstTime, bool hasAuthority)
        => firstTime && ShouldReplaceNativeSpawn(clanId, hasAuthority);

    internal static IReadOnlyList<CharacterObject> GetEnabledTemplates(
        IEnumerable<CharacterObject> templates)
    {
        if (templates == null) return Array.Empty<CharacterObject>();

        return templates
            .Where(template => template?.IsTemplate == true)
            .ToArray();
    }

    private static void CreateInitialHeroes(Clan clan)
    {
        Hero configuredLeader = null;
        foreach (CharacterObject template in GetEnabledTemplates(clan.MinorFactionCharacterTemplates))
        {
            Hero hero = CreateHeroFromTemplate(template, clan);
            if (string.Equals(
                    template.StringId,
                    CoopTeamLeaderTemplateId,
                    StringComparison.Ordinal))
                configuredLeader = hero;
        }

        if (configuredLeader != null)
            clan.SetLeader(configuredLeader);
    }

    internal static Hero CreateHeroFromTemplate(CharacterObject template, Clan clan)
    {
        int age = Campaign.Current.GameStarted ? 19 : -1;
        var (birthDay, deathDay) = Campaign.Current.Models.HeroCreationModel
            .GetBirthAndDeathDay(template, createAlive: true, age);
        Hero hero = HeroCreator.CreateHero(
            template,
            useCharacterAsTemplate: true,
            birthDay,
            deathDay);

        var initializationArgs = new HeroCreator.HeroInitializationArgs(hero, isOffspring: false)
            .SetGenerateFirstAndFullName(value: false)
            .SetName(template.Name.CopyTextObject())
            .SetFirstName(template.Name.CopyTextObject())
            .SetClan(clan);
        HeroCreator.InitializeHeroFromSettings(initializationArgs.Hero, initializationArgs);

        hero.ChangeState(
            Campaign.Current.GameStarted
                ? Hero.CharacterStates.Active
                : Hero.CharacterStates.NotSpawned);
        hero.IsMinorFactionHero = true;
        return hero;
    }
}
