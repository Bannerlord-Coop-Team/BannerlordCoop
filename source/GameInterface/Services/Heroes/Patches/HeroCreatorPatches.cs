using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

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

        var message = new InitializeNewHero(hero);
        MessageBroker.Instance.Publish(null, message);
    }

    [HarmonyPatch(nameof(HeroCreator.CreateSpecialHero), new[]
    {
        typeof(CharacterObject),
        typeof(Settlement),
        typeof(Clan),
        typeof(Clan),
        typeof(int),
    })]
    [HarmonyPostfix]
    public static void CreateSpecialHeroPostfix(CharacterObject template, Hero __result)
    {
        if (!IsConfiguredMinorFactionLordTemplate(template)) return;

        bool isServer = ModInformation.IsServer;
        bool originalAllowed = !isServer && CallOriginalPolicy.IsOriginalAllowed();
        if (!ShouldApplyConfiguredName(isServer, originalAllowed)) return;

        ApplyConfiguredMinorFactionLordName(
            __result,
            template,
            synchronize: isServer);
    }

    internal static bool ShouldApplyConfiguredName(bool isServer, bool originalAllowed)
        => isServer || originalAllowed;

    private static bool IsConfiguredMinorFactionLordTemplate(CharacterObject template)
    {
        return template?.StringId?.StartsWith(ConfiguredMinorFactionLordPrefix, StringComparison.Ordinal) == true &&
               template.Occupation == Occupation.Lord &&
               template.Name != null;
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

    internal static void ApplyConfiguredMinorFactionLordName(
        Hero hero,
        CharacterObject template = null,
        bool synchronize = false)
    {
        template ??= hero?.Template;
        if (!IsConfiguredMinorFactionLordTemplate(template)) return;

        var fullName = template.Name.CopyTextObject();
        var firstName = fullName.CopyTextObject();
        if (synchronize)
            hero.SetName(fullName, firstName);
        else
            HeroDataPatches.SetNameOverride(hero, fullName, firstName);
    }
}
