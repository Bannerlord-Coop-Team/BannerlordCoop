using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Patches
{
    [HarmonyPatch]
    internal class BesiegerCampCollectionPatches : GenericPatches<BesiegerCampCollectionPatches, BesiegerCamp>
    {
        private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(BesiegerCamp));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> BesiegerPartiesTranspiler(IEnumerable<CodeInstruction> instructions)
            => ListFieldChangeTranspiler<MobileParty, BesiegerPartyAdded, BesiegerPartyRemoved>(instructions, nameof(BesiegerCamp._besiegerParties));
    }
}