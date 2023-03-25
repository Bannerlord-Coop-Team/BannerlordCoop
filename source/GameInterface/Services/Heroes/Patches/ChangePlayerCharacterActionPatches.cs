using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
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
            MessageBroker.Instance.Publish(null, new PlayerHeroChanged(Hero.MainHero, hero));
        }
    }
}
