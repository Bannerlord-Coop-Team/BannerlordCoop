using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal class ClanKingdomPatches
{
    [HarmonyPatch(typeof(CampaignEvents), nameof(CampaignEvents.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void OnClanChangedKingdomPostfix(CampaignEvents __instance, Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (ModInformation.IsClient) return;
        MessageBroker.Instance.Publish(__instance, new OnClanChangedKingdom(clan, oldKingdom, newKingdom, detail));
    }
    [HarmonyPatch(typeof(DefaultCutscenesCampaignBehavior), nameof(DefaultCutscenesCampaignBehavior.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool Prefix(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        return ModInformation.IsClient;
    }
}
