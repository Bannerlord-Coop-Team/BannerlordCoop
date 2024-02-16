using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class PartyArmyPatches
{
    [HarmonyPatch(nameof(MobileParty.Army), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetArmyPrefix(MobileParty __instance, Army value)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var data = new PartyArmyChangeData(__instance.StringId, value.GetStringId());
        var message = new PartyArmyChanged(data);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    internal static void OverrideSetArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using(new AllowedThread())
            {
                mobileParty.Army = army;
            }
        });
    }
}
