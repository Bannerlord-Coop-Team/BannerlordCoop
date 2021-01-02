using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;

namespace Coop.Mod.Patch.Party
{
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "CheckRecruiting")]
    class RecruitmentCampaignBehaviorPatch
    {
        static bool Prefix(ref MobileParty mobileParty, ref Settlement settlement)
        {
            if ((CoopServer.Instance?.Persistence?.EntityManager?.PlayerControllerParties) != null)
            {
                // Skip function if mobile party is player controlled
                return !CoopServer.Instance.Persistence.EntityManager.PlayerControllerParties.ContainsValue(mobileParty);
            }
            return true;
        }
    }
}
