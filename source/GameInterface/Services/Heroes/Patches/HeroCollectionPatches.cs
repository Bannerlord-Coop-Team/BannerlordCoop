using Common.Logging;
using Common.Messaging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using GameInterface.Policies;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Services.Heroes.Messages.Collections;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using GameInterface.Services.TroopRosters.Handlers;
using GameInterface.Utils;
using System.Collections;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch]
internal class HeroCollectionPatches : GenericCollectionPatches<HeroCollectionPatches, Hero>
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroCollectionPatches>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Hero)))
        {
            yield return method;
        }
        yield return AccessTools.Method(typeof(RecruitmentVM), nameof(RecruitmentVM.OnDone));
        yield return AccessTools.Method(typeof(RecruitmentCampaignBehavior), nameof(RecruitmentCampaignBehavior.RecruitVolunteersFromNotable));
        yield return AccessTools.Method(typeof(RecruitmentCampaignBehavior), nameof(RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement));
        yield return AccessTools.Method(typeof(RecruitmentCampaignBehavior), nameof(RecruitmentCampaignBehavior.ApplyInternal));
        yield return AccessTools.Method(typeof(Town), nameof(Town.DailyGarrisonAdjustment));
        // Carvans
        yield return AccessTools.Method(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.OnFinalize));
        yield return AccessTools.Method(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.OnInitialize));
        // Alleys
        yield return AccessTools.Method(typeof(Alley), nameof(Alley.AfterLoad));
        yield return AccessTools.Method(typeof(Alley), nameof(Alley.SetOwner));
        //yield return AccessTools.Method(typeof(TroopRosterHandler), nameof(TroopRosterHandler.HandleOnRecruitmentDone));
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> VolunteerTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var heroLoadStack = new Stack<CodeInstruction>();
        CodeInstruction previous = null;
        var VolunteerArrayType = AccessTools.Field(typeof(Hero), nameof(Hero.VolunteerTypes));
        var arrayAssignIntercept = AccessTools.Method(typeof(HeroCollectionPatches), nameof(ArrayAssignIntercept));

        foreach (var instruction in instructions)
        {
            // TODO: Check if this is actually necessary or if the full generic approach is usable
            // Track Hero load instructions before accessing VolunteerTypes
            if (instruction.opcode == OpCodes.Ldfld && (FieldInfo)instruction.operand == VolunteerArrayType)
            {
                if (previous != null && IsLdloc(previous))
                {
                    heroLoadStack.Push(previous);
                }
                else
                {
                    heroLoadStack.Push(null);
                }
            }

            // Replace `stelem.ref` with intercept call
            if (instruction.opcode == OpCodes.Stelem_Ref)
            {
                if (heroLoadStack.Count > 0)
                {
                    var heroLoad = heroLoadStack.Pop();
                    if (heroLoad != null)
                    {
                        yield return heroLoad; // Inject Hero instance
                        yield return new CodeInstruction(OpCodes.Call, arrayAssignIntercept) { labels = instruction.labels };
                        continue;
                    }
                }
            }

            yield return instruction;
            previous = instruction; // Track previous instruction
        }
    }

    // Helper method to check if an instruction loads a local variable
    private static bool IsLdloc(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldloc || instruction.opcode == OpCodes.Ldloc_S ||
               instruction.opcode == OpCodes.Ldloc_0 || instruction.opcode == OpCodes.Ldloc_1 ||
               instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Ldloc_3;
    }

    public static void ArrayAssignIntercept(CharacterObject[] VolunteerTypes, int index, CharacterObject value, Hero instance)
        => ArrayAssignIntercept<CharacterObject, VolunteerTypesArrayUpdated>(VolunteerTypes, index, value, instance);

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ChildrenTranspiler(IEnumerable<CodeInstruction> instructions) 
        => MBListTranspiler<Hero, ChildrenListUpdated, ChildrenListRemoved>(instructions);
    
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> CaravanTranspiler(IEnumerable<CodeInstruction> instructions) 
        => ListTranspiler<CaravanPartyComponent, CaravanListUpdated, CaravanListRemoved>(instructions);
   
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> AlleyTranspiler(IEnumerable<CodeInstruction> instructions)
        => ListTranspiler<Alley, AlleyListUpdated, AlleyListRemoved>(instructions);

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> WorkshopTranspiler(IEnumerable<CodeInstruction> instructions)
        => MBListTranspiler<Workshop, WorkshopListUpdated, WorkshopListRemoved>(instructions);
}


