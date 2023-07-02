using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Hideouts.Patches;

[HarmonyPatch(typeof(HideoutConversationsCampaignBehavior))]
internal class DisableHideoutConversationsCampaignBehavior
{
    [HarmonyPatch(nameof(HideoutConversationsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
