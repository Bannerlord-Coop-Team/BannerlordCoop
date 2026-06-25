using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(CharacterRelationManager))]
internal class CharacterRelationManagerPatches
{
    [HarmonyPatch(nameof(CharacterRelationManager.SetHeroRelation))]
    [HarmonyPostfix]
    private static void Postfix_SetHeroRelation(Hero hero1, Hero hero2, int value)
    {
        if (!ModInformation.IsServer) return;

        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (string.IsNullOrEmpty(hero1?.StringId) || string.IsNullOrEmpty(hero2?.StringId)) return;

        MessageBroker.Instance.Publish(hero1, new HeroRelationChanged(hero1.StringId, hero2.StringId, value));
    }
}
