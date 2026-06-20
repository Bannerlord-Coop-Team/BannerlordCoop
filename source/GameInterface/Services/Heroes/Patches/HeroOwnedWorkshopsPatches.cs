using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Hero))]
internal class HeroOwnedWorkshopsPatches
{
    [HarmonyPatch(nameof(Hero.AddOwnedWorkshop))]
    [HarmonyPrefix]
    public static bool AddOwnedWorkshopPrefix(ref Hero __instance, Workshop workshop)
    {
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        var message = new OwnedWorkshopAdded(__instance, workshop);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(Hero.RemoveOwnedWorkshop))]
    [HarmonyPrefix]
    public static bool RemoveOwnedWorkshopPrefix(ref Hero __instance, Workshop workshop)
    {
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        var message = new OwnedWorkshopRemoved(__instance, workshop);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
