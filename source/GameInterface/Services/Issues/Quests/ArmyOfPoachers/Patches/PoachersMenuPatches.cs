using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers.Patches;

using Behavior = MerchantArmyOfPoachersIssueBehavior;
using Quest = MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

[HarmonyPatch(typeof(Behavior))]
internal class PoachersMenuPatches
{
    [HarmonyPatch("Instance", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool InstancePrefix(ref Quest __result)
    {
        __result = null;
        if (ModInformation.IsClient && ContainerProvider.TryResolve<PoachersEncounterHandler>(out var encounters))
            __result = Campaign.Current.QuestManager.Quests.OfType<Quest>()
                .FirstOrDefault(quest => quest.IsOngoing && encounters.IsLocalOwner(quest));
        return false;
    }

    [HarmonyPatch("OnSessionLaunched")]
    [HarmonyPrefix]
    private static bool SessionLaunchedPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(Behavior.OnCheckForIssue))]
    [HarmonyPrefix]
    private static bool CheckForIssuePrefix() => ModInformation.IsServer;

    [HarmonyPatch("army_of_poachers_village_on_init")]
    [HarmonyPrefix]
    private static bool MenuInitPrefix(MenuCallbackArgs args)
    {
        var quest = Behavior.Instance;
        if (quest == null || !quest.IsOngoing) return false;
        args.MenuContext.SetBackgroundMeshName(quest._questVillage.Settlement.SettlementComponent.WaitMeshName);
        if (quest._isReadyToBeFinalized)
        {
            // The shared battle path finalizes the server quest after its authoritative result.
            PlayerEncounter.Update();
            return false;
        }
        if (quest._poachersParty == null && !Hero.MainHero.IsWounded) quest.CreatePoachersParty();
        if (quest._talkedToPoachersBattleWillStart) quest.StartQuestBattle();
        return false;
    }

    [HarmonyPatch("engage_poachers_condition")]
    [HarmonyPostfix]
    private static void FightReadyPostfix(MenuCallbackArgs args) => CheckPartyReady(args);

    [HarmonyPatch("talk_to_leader_of_poachers_condition")]
    [HarmonyPostfix]
    private static void TalkReadyPostfix(MenuCallbackArgs args) => CheckPartyReady(args);

    private static void CheckPartyReady(MenuCallbackArgs args)
    {
        if (Behavior.Instance?._poachersParty != null) return;
        args.IsEnabled = false;
        args.Tooltip = new TextObject("{=CoopPoachersPreparing}Waiting for the poachers party.");
    }

    [HarmonyPatch("poachers_menu_back_condition")]
    [HarmonyPostfix]
    private static void BackWhileWaitingPostfix(ref bool __result)
    {
        if (Behavior.Instance is Quest quest && quest._poachersParty == null)
            __result = true;
    }
}

[HarmonyPatch(typeof(Quest))]
internal class PoachersEncounterPatches
{
    [HarmonyPatch(nameof(Quest.CreatePoachersParty))]
    [HarmonyPrefix]
    private static bool CreatePartyPrefix(Quest __instance)
    {
        if (ModInformation.IsServer) return true;
        if (ContainerProvider.TryResolve<PoachersEncounterHandler>(out var encounters))
            encounters.RequestParty(__instance);
        return false;
    }

    [HarmonyPatch(nameof(Quest.CreatePoachersParty))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CreatePartyTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.CurrentSettlement));
        var replacement = AccessTools.Method(typeof(PoachersEncounterPatches), nameof(GetQuestSettlement));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }

    private static Settlement GetQuestSettlement(Quest quest) => quest._questVillage.Settlement;

    [HarmonyPatch(nameof(Quest.StartQuestBattle))]
    [HarmonyPrefix]
    private static bool StartBattlePrefix(Quest __instance)
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<PoachersEncounterHandler>(out var encounters))
            encounters.StartBattle(__instance);
        return false;
    }

    [HarmonyPatch("HourlyTick")]
    [HarmonyPrefix]
    private static bool HourlyTickPrefix(Quest __instance) => IsLocalOwner(__instance);

    [HarmonyPatch("GameMenuOpened")]
    [HarmonyPrefix]
    private static bool GameMenuOpenedPrefix(Quest __instance) => IsLocalOwner(__instance);

    [HarmonyPatch("SetStartDialogOnCondition")]
    [HarmonyPrefix]
    private static bool StartDialogPrefix(Quest __instance, ref bool __result)
    {
        __result = false;
        return IsLocalOwner(__instance);
    }

    private static bool IsLocalOwner(Quest quest) => ModInformation.IsClient && quest.IsOngoing &&
        ContainerProvider.TryResolve<PoachersEncounterHandler>(out var encounters) && encounters.IsLocalOwner(quest);
}
