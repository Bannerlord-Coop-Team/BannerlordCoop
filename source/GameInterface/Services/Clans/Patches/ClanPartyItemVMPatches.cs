using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core.ViewModelCollection.Selector;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanPartyItemVM))]
internal class ClanPartyItemVMPatches
{
    [HarmonyPatch(nameof(ClanPartyItemVM.UpdateProperties))]
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> UpdatePropertiesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var clanGetter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.Clan));
        var clanLeaderGetter = AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.Leader));
        var clanLeaderOrSelf = AccessTools.Method(typeof(ClanPartyItemVMPatches), nameof(GetClanLeaderOrSelf));
        var instructionList = instructions.ToList();
        var replacementCount = 0;

        for (int i = 0; i < instructionList.Count; i++)
        {
            var instruction = instructionList[i];
            if (i + 1 < instructionList.Count &&
                instruction.Calls(clanGetter) &&
                instructionList[i + 1].Calls(clanLeaderGetter))
            {
                var duplicateLeader = new CodeInstruction(OpCodes.Dup);
                duplicateLeader.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                duplicateLeader.blocks.AddRange(instruction.blocks);
                instruction.blocks.Clear();
                yield return duplicateLeader;
                yield return instruction;

                var clanLeaderInstruction = instructionList[++i];
                clanLeaderInstruction.opcode = OpCodes.Call;
                clanLeaderInstruction.operand = clanLeaderOrSelf;
                yield return clanLeaderInstruction;
                replacementCount++;
                continue;
            }

            yield return instruction;
        }

        if (replacementCount != 1)
            throw new InvalidOperationException($"Expected one clan leader lookup in {nameof(ClanPartyItemVM.UpdateProperties)}, found {replacementCount}.");
    }

    internal static Hero GetClanLeaderOrSelf(Hero partyLeader, Clan clan)
    {
        return clan?.Leader ?? partyLeader;
    }

    [HarmonyPatch(nameof(ClanPartyItemVM.UpdatePartyBehaviorSelectionUpdate))]
    [HarmonyPrefix]
    public static bool UpdatePartyBehaviorSelectionUpdatePrefix(ref ClanPartyItemVM __instance, SelectorVM<SelectorItemVM> s)
    {
        if (s.SelectedIndex != (int)__instance.Party.MobileParty.Objective)
        {
            // Manage setting the party behavior on the server
            var message = new PartyBehaviorUpdatedOnSelection(__instance.Party.MobileParty, (MobileParty.PartyObjective)s.SelectedIndex);
            MessageBroker.Instance.Publish(__instance, message);
        }

        return false;
    }
    
    [HarmonyPatch(nameof(ClanPartyItemVM.OnAutoRecruitChanged))]
    [HarmonyPrefix]
    public static bool OnAutoRecruitChangedPrefix(ref ClanPartyItemVM __instance, bool value)
    {
        if (__instance.Party.IsMobile && __instance.Party.MobileParty.IsGarrison)
        {
            Settlement homeSettlement = __instance.Party.MobileParty.HomeSettlement;
            if (homeSettlement?.Town != null)
            {
                // Manage setting auto recruitment on the server
                var message = new AutoRecruitChangedForSettlement(__instance.Party.MobileParty.HomeSettlement, value);
                MessageBroker.Instance.Publish(__instance, message);
            }
        }

        return false;
    }

}
