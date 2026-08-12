using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroCreator))]
internal class HeroCreatorPatches
{
    internal const string ConfiguredWandererPrefix = "coop_wanderer_";
    internal const string ConfiguredMinorFactionLordPrefix = "coop_team_lord_";

    [HarmonyPatch(nameof(HeroCreator.InitializeHeroFromSettings))]
    [HarmonyPostfix]
    public static void InitializeHeroFromSettingsPostfix(Hero hero, HeroCreator.HeroInitializationArgs initializationArgs)
    {
        if (ModInformation.IsClient) return;

        ApplyConfiguredWandererName(hero);
        ApplyConfiguredMinorFactionLordName(hero);

        var message = new InitializeNewHero(hero);
        MessageBroker.Instance.Publish(null, message);
    }

    internal static void ApplyConfiguredWandererName(Hero hero)
    {
        var template = hero?.Template;
        if (template?.StringId?.StartsWith(ConfiguredWandererPrefix, StringComparison.Ordinal) != true)
            return;
        if (template.Occupation != Occupation.Wanderer || template.Name == null)
            return;

        var fullName = template.Name.CopyTextObject();
        HeroDataPatches.SetNameOverride(hero, fullName, fullName.CopyTextObject());
    }

    internal static void ApplyConfiguredMinorFactionLordName(Hero hero)
    {
        var template = hero?.Template;
        if (template?.StringId?.StartsWith(ConfiguredMinorFactionLordPrefix, StringComparison.Ordinal) != true)
            return;
        if (template.Occupation != Occupation.Lord || template.Name == null)
            return;

        var fullName = template.Name.CopyTextObject();
        HeroDataPatches.SetNameOverride(hero, fullName, fullName.CopyTextObject());
    }
}
