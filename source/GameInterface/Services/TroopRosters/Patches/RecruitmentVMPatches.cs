using Common;
using Common.Messaging;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using GameInterface.Policies;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(RecruitmentVM))]
public class RecruitmentVMPatches
{
    [HarmonyPatch("OnDone")]
    [HarmonyPrefix]
    public static bool OnDonePrefix(ref RecruitmentVM __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        var troopsInCart = __instance.TroopsInCart.Select(t => (t.Owner.OwnerHero, t.Character, t.Index)).ToArray();

        var message = new RecruitmentAttempted(MobileParty.MainParty, troopsInCart);

        MessageBroker.Instance.Publish(__instance, message);

        __instance.Deactivate();

        return false;
    }
}