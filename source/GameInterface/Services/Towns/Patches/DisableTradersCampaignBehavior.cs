using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(TradersCampaignBehavior))]
internal class DisableTradersCampaignBehavior
{
    [HarmonyPatch(nameof(TradersCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
