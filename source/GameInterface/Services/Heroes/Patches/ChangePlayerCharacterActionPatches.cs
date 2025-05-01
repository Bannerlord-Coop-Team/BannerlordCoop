using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(ChangePlayerCharacterAction))]
internal class ChangePlayerCharacterActionPatches
{
    [HarmonyPatch("Apply")]
    private static void Prefix(Hero hero)
    {
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(null, new PlayerHeroChanged(Hero.MainHero, hero));
    }
}
