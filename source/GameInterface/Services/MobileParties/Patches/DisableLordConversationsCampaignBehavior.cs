using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class DisableLordConversationsCampaignBehavior
{
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
