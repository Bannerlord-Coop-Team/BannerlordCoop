using Coop.Mod.Extentions;
using GameInterface.Extentions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using System.Linq;
using TaleWorlds.Library;
using Common.Messaging;
using GameInterface.Utils;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
static class DisablePartyDecisionMaking
{
    /// <summary>
    /// Updates the GetBehaviors method to avoid the NPC code section from being applied to player controlled parties.
    /// </summary>
    /*[HarmonyTranspiler]
    [HarmonyPatch("GetBehaviors")]
    public static IEnumerable<CodeInstruction> GetBehaviorTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var isAnyPlayerMainParty = AccessTools.Method(typeof(MobilePartyExtensions), nameof(MobilePartyExtensions.IsAnyPlayerMainParty));
        var mobilePartyField = AccessTools.Field(typeof(MobilePartyAi), "_mobileParty");

        for (int i = 0; i < codes.Count; i++)
        {
            // Look for the start of the if statement.
            if (codes[i].opcode == OpCodes.Call &&
                codes[i].operand is MethodInfo &&
                (MethodInfo)codes[i].operand == AccessTools.Property(typeof(Campaign), nameof(Campaign.Current)).GetGetMethod(false))
            {
                // Add our coundtion: this._mobileParties.IsAnyPlayerMainParty()
                codes.InsertRange(i + 3, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, mobilePartyField),
                    new CodeInstruction(OpCodes.Call, isAnyPlayerMainParty),
                     // Look ahead for the operand of the existing Brfalse and use the same destination
                    new CodeInstruction(OpCodes.Brfalse, codes[i + 2].operand),
                });

                break;
            }
        }

        return codes.AsEnumerable();
    }*/

    /*[HarmonyPrefix]
    [HarmonyPatch("GetBehaviors")]
    static bool PrefixGetBehaviors(MobilePartyAi __instance, ref AiBehavior bestAiBehavior, ref PartyBase behaviorParty, ref Vec2 bestTargetPoint)
    {
        MobileParty party = __instance.GetMobileParty();
        if (party.IsAnyPlayerMainParty())
        {
            bestAiBehavior = party.DefaultBehavior;
            bestTargetPoint = party.TargetPosition;
            behaviorParty = null;

            if (bestAiBehavior == AiBehavior.GoToSettlement)
            {
                behaviorParty = party.TargetSettlement.Party;
            } 
            else if (party.TargetParty != null)
            {
                behaviorParty = party.TargetParty.Party;
            }

            return false;
        }

        return true;
    }*/

    static MobilePartyAi AllowedTickInternal;

    public static void TickInternalOverride(MobilePartyAi partyAi)
    {
        AllowedTickInternal = partyAi;
        lock (AllowedTickInternal)
        {
            ReflectionUtils.InvokePrivateMethod(typeof(MobilePartyAi), "TickInternal", partyAi);
        }
        AllowedTickInternal = null;
    }

    [HarmonyPrefix]
    [HarmonyPatch("TickInternal")]
    static bool PrefixTickInternal(ref MobilePartyAi __instance)
    {
        if (AllowedTickInternal == __instance)
        {
            return true;
        }

        if (ModInformation.IsClient && !__instance.GetMobileParty().IsMainParty)
        {
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new RequestTickInternal(__instance));

        return false;
    }

    /*[HarmonyPrefix]
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsMainParty), MethodType.Getter)]
    static bool PrefixIsMainPartyGetter(ref MobileParty __instance, ref bool __result)
    {
        __result = __instance.IsAnyPlayerMainParty();
        return false;
    }*/

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MobilePartyAi.DoNotMakeNewDecisions), MethodType.Getter)]
    static bool PrefixDoNotMakeNewDecisionsGetter(MobilePartyAi __instance, ref bool __result)
    {
        MobileParty party = __instance.GetMobileParty();
        if (party.IsAnyPlayerMainParty() || ModInformation.IsClient)
        {
            // Disable decision making for parties our client doesn't control. Decisions are made remote.
            __result = true;
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SetPartyAiAction), "ApplyInternal", MethodType.Normal)]
    static bool Prefix(ref MobileParty owner)
    {
        if (owner.IsAnyPlayerMainParty() || ModInformation.IsClient)
        {
            return false;
        }

        return true;
    }

    
}