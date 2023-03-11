using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Heroes.Patches
{
    [HarmonyPatch(typeof(ChangePlayerCharacterAction))]
    internal class ChangePlayerCharacterActionPatches
    {
        [HarmonyPatch("Apply")]
        private static void Prefix(Hero hero)
        {
            // Remove previous controlled hero if there was one
            ControlledHeroRegistry.RemoveControlledHero(Hero.MainHero);

            // Register new hero as controlled
            ControlledHeroRegistry.RegisterControlledHero(hero);
        }
    }
}
