using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Hero))]
internal class HeroMetPatches
{
    [HarmonyPatch(nameof(Hero.SetHasMet))]
    [HarmonyPostfix]
    public static void SetHasMetPostfix(Hero __instance)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return;
        var playerHero = Hero.MainHero;
        if (playerHero == null) return;

        MessageBroker.Instance.Publish(
            __instance,
            new PlayerMetHero(playerHero, __instance, __instance.LastMeetingTimeWithPlayer));
    }

    // Replace this later with a dictionary to properly manage each player knowing of every hero
    // HeroKnownInformationCampaignBehavior is what mostly manages this property
    [HarmonyPatch(nameof(Hero.IsKnownToPlayer), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool IsKnownToPlayerGetterPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
