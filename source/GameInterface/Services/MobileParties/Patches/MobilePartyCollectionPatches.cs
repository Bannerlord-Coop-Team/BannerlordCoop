using GameInterface.Services.MobileParties.Messages;
using GameInterface.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class MobilePartyCollectionPatches : GenericPatches<MobilePartyCollectionPatches, MobileParty>
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(MobileParty))
            .Where(method => method.Name != nameof(MobileParty.CheckAiForMapChangeAndUpdateIfNeeded));
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => ListFieldChangeTranspiler<MobileParty, AttachedPartyAdded, AttachedPartyRemoved>(instructions, nameof(MobileParty._attachedParties));
}
