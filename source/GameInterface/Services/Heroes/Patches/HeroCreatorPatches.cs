using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroCreator))]
internal class HeroCreatorPatches
{
    [HarmonyPatch(nameof(HeroCreator.InitializeHeroFromSettings))]
    [HarmonyPostfix]
    public static void InitializeHeroFromSettingsPostfix(Hero hero, HeroCreator.HeroInitializationArgs initializationArgs)
    {
        if (ModInformation.IsClient) return;

        var message = new InitializeNewHero(hero);
        MessageBroker.Instance.Publish(null, message);
    }
}
