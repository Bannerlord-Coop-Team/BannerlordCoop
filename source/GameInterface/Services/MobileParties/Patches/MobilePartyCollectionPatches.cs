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
    new static IEnumerable<MethodBase> TargetMethods()
    {
        return GenericPatches<MobilePartyCollectionPatches, MobileParty>.TargetMethods();
    }

    [HarmonyPrepare]
    private static bool Prepare() => TargetMethods().Any();

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => ListFieldChangeTranspiler<MobileParty, AttachedPartyAdded, AttachedPartyRemoved>(instructions, nameof(MobileParty._attachedParties));
}
