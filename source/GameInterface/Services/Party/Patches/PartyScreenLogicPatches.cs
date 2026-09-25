using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Villages;
using HarmonyLib;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using MathF = TaleWorlds.Library.MathF;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenLogic))]
internal class PartyScreenLogicPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenLogic>();
    [ThreadStatic]
    private static bool _inCommit;
    [ThreadStatic]
    private static PartyScreenLogic _currentQuestScreen;
    internal static PartyScreenLogic CurrentQuestScreen => _currentQuestScreen;
    internal static bool InCommit
    {
        get => _inCommit;
        private set => _inCommit = value;
    }

    [HarmonyPatch(nameof(PartyScreenLogic.ValidateCommand))]
    [HarmonyPrefix]
    public static bool ValidateCommandPrefix(PartyScreenLogic.PartyCommand command, ref bool __result)
    {
        // Force-transfer loot screens only honor member takes and dismissals: the
        // commit validation rejects any prisoner, upgrade, gold, influence, or
        // morale movement, which would fail after the screen already reset.
        // Block those operations up front so the buttons simply stay disabled.
        // ValidateCommand is also queried per-frame by the UI, so no message.
        // Member transfers stay allowed (takes and dismissals); shifts and sorts
        // only reorder and never affect the commit deltas.
        if (ForceTransferScreenTracker.HasOpenForceTransferScreen() &&
            IsBlockedOnForceTransferScreen(command.Code, command.Type))
        {
            __result = false;
            return false;
        }

        return true;
    }

    internal static bool IsBlockedOnForceTransferScreen(PartyScreenLogic.PartyCommandCode code, PartyScreenLogic.TroopType type)
    {
        switch (code)
        {
            case PartyScreenLogic.PartyCommandCode.UpgradeTroop:
            case PartyScreenLogic.PartyCommandCode.RecruitTroop:
            case PartyScreenLogic.PartyCommandCode.ExecuteTroop:
            case PartyScreenLogic.PartyCommandCode.TransferPartyLeaderTroop:
                return true;
            case PartyScreenLogic.PartyCommandCode.TransferTroop:
            case PartyScreenLogic.PartyCommandCode.TransferTroopToLeaderSlot:
            case PartyScreenLogic.PartyCommandCode.TransferAllTroops:
                return (type & PartyScreenLogic.TroopType.Prisoner) != 0;
            default:
                return false;
        }
    }

    [HarmonyPatch("TransferTroop")]
    [HarmonyPrefix]
    private static void TransferTroopPrefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command,
        out (TroopRoster Roster, TroopRoster Before, CharacterObject Character, int Count, int Wounded, int Xp) __state)
    {
        __state = default;
        if (!ModInformation.IsClient || __instance._partyScreenMode != PartyScreenHelper.PartyScreenMode.QuestTroopManage ||
            command.Character == null || MobileParty.MainParty == null ||
            command.Type != PartyScreenLogic.TroopType.Member ||
            __instance.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] != MobileParty.MainParty.MemberRoster)
            return;

        var roster = __instance.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left];
        if (roster == null) return;
        var element = ReadTroopElement(roster, command.Character);
        __state = (roster, CopyRoster(roster), command.Character, element.Count, element.Wounded, element.Xp);
    }

    [HarmonyPatch("TransferTroop")]
    [HarmonyPostfix]
    private static void TransferTroopPostfix(
        (TroopRoster Roster, TroopRoster Before, CharacterObject Character, int Count, int Wounded, int Xp) __state)
    {
        if (__state.Roster == null) return;
        var element = ReadTroopElement(__state.Roster, __state.Character);
        if (element.Count == __state.Count && element.Wounded == __state.Wounded && element.Xp == __state.Xp) return;

        MessageBroker.Instance.Publish(__state.Roster, new QuestAlternativeTroopsTransferredLocally(
            __state.Roster, __state.Before, CopyRoster(__state.Roster)));
    }

    [HarmonyPatch("TransferTroopToLeaderSlot")]
    [HarmonyPrefix]
    private static void TransferTroopToLeaderSlotPrefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command,
        out (TroopRoster Roster, TroopRoster Before, CharacterObject Character, int Count, int Wounded, int Xp) __state)
        => TransferTroopPrefix(__instance, command, out __state);

    [HarmonyPatch("TransferTroopToLeaderSlot")]
    [HarmonyPostfix]
    private static void TransferTroopToLeaderSlotPostfix(
        (TroopRoster Roster, TroopRoster Before, CharacterObject Character, int Count, int Wounded, int Xp) __state)
        => TransferTroopPostfix(__state);

    private static TroopRoster CopyRoster(TroopRoster roster)
    {
        var copy = TroopRoster.CreateDummyTroopRoster();
        copy.Add(roster);
        return copy;
    }

    private static (int Count, int Wounded, int Xp) ReadTroopElement(TroopRoster roster, CharacterObject character)
    {
        var index = roster.FindIndexOfTroop(character);
        if (index < 0) return default;
        var element = roster.GetElementCopyAtIndex(index);
        return (element.Number, element.WoundedNumber, element.Xp);
    }

    [HarmonyPatch(nameof(PartyScreenLogic.DoneLogic))]
    [HarmonyPrefix]
    public static bool DoneLogicPrefix(PartyScreenLogic __instance, ref bool __result, bool isForced)
    {
        if (Hero.MainHero.Gold < -__instance.CurrentData.PartyGoldChangeAmount && __instance.CurrentData.PartyGoldChangeAmount < 0)
        {
            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_inventory_popup_player_not_enough_gold", null), 0, null, null, "");
            __result = false;
            return false;
        }

        FlattenedTroopRoster releasedPrisonersRoster = new FlattenedTroopRoster(4);
        FlattenedTroopRoster takenPrisonersRoster = new FlattenedTroopRoster(4);
        foreach (Tuple<CharacterObject, int> tuple in __instance.CurrentData.TransferredPrisonersHistory)
        {
            int number = MathF.Abs(tuple.Item2);
            if (tuple.Item2 < 0)
            {
                releasedPrisonersRoster.Add(tuple.Item1, number, 0);
            }
            else if (tuple.Item2 > 0)
            {
                takenPrisonersRoster.Add(tuple.Item1, number, 0);
            }
        }

        PartyScreenHelperPatches.ResetReleasedAndTakenPrisonerActionsRequest();
        PartyScreenHelperPatches.ResetPrisonerDonationRequest();
        var previousQuestScreen = _currentQuestScreen;
        if (ModInformation.IsClient && __instance._partyScreenMode == PartyScreenHelper.PartyScreenMode.QuestTroopManage)
            _currentQuestScreen = __instance;
        bool flag;
        try
        {
            flag = __instance.PartyPresentationDoneButtonDelegate(__instance.MemberRosters[0], __instance.PrisonerRosters[0], __instance.MemberRosters[1], __instance.PrisonerRosters[1], takenPrisonersRoster, releasedPrisonersRoster, isForced, __instance.LeftOwnerParty, __instance.RightOwnerParty);
        }
        finally
        {
            _currentQuestScreen = previousQuestScreen;
        }
        bool applyReleasedAndTakenPrisonerActions =
            PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest();
        PartyScreenHelperPatches.ConsumePrisonerDonationRequest(
            out var donationSettlement,
            out var donatedPrisonersRoster);
        if (flag)
        {
            var questSelectionRoster = ModInformation.IsClient &&
                __instance._partyScreenMode == PartyScreenHelper.PartyScreenMode.QuestTroopManage &&
                __instance.CurrentData != __instance._initialData &&
                __instance.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] == MobileParty.MainParty?.MemberRoster
                ? __instance.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] : null;
            var questSelectionSnapshot = questSelectionRoster == null ? null : CopyRoster(questSelectionRoster);
            FlattenedTroopRoster recruitedPrisonersRoster = new FlattenedTroopRoster(4);
            foreach (Tuple<CharacterObject, int> tuple in __instance.CurrentData.RecruitedPrisonersHistory)
            {
                recruitedPrisonersRoster.Add(tuple.Item1, tuple.Item2, 0);
            }

            var partyScreenMode = __instance._partyScreenMode;
            if (Game.Current.GameStateManager.ActiveState is PartyState partyState)
            {
                partyScreenMode = partyState.PartyScreenMode;
            }

            ForceTransferScreenTracker.TryClaimForceTransferId(__instance.MemberRosters[0], out var forceTransferId);
            var message = new PartyDoneLogicAttempted(
                Hero.MainHero,
                releasedPrisonersRoster,
                takenPrisonersRoster,
                recruitedPrisonersRoster,
                __instance.MemberRosters[0],
                __instance.PrisonerRosters[0],
                __instance.MemberRosters[1],
                __instance.PrisonerRosters[1],
                __instance._initialData.LeftMemberRoster,
                __instance._initialData.LeftPrisonerRoster,
                __instance._initialData.RightMemberRoster,
                __instance._initialData.RightPrisonerRoster,
                __instance.RightOwnerParty.ItemRoster,
                __instance.CurrentData.UpgradedTroopsHistory,
                __instance.CurrentData.LeftParty,
                __instance.CurrentData.PartyGoldChangeAmount,
                __instance.CurrentData.PartyInfluenceChangeAmount.Item2,
                __instance.CurrentData.PartyMoraleChangeAmount,
                __instance.DoNotApplyGoldTransactions,
                partyScreenMode,
                applyReleasedAndTakenPrisonerActions,
                donationSettlement,
                donatedPrisonersRoster,
                forceTransferId
            );

            MessageBroker.Instance.Publish(__instance, message);
            // Manage changing rosters on the server
            using (new AllowedThread())
            {
                TroopRoster duplicateLeftMemberRoster = __instance.MemberRosters[0].CloneRosterData();
                TroopRoster duplicateLeftPrisonerRoster = __instance.PrisonerRosters[0].CloneRosterData();

                InCommit = true;
                try
                {
                    __instance.Reset(true);

                    //__instance.FireCampaignRelatedEvents(); // Managed on server
                    __instance.SetPartyGoldChangeAmount(0);
                    __instance.SetHorseChangeAmount(0);
                    __instance.SetInfluenceChangeAmount(0, 0, 0);
                    __instance.SetMoraleChangeAmount(0);
                    __instance.CurrentData.UpgradedTroopsHistory = new List<Tuple<CharacterObject, CharacterObject, int>>();
                    __instance.CurrentData.TransferredPrisonersHistory = new List<Tuple<CharacterObject, int>>();
                    __instance.CurrentData.RecruitedPrisonersHistory = new List<Tuple<CharacterObject, int>>();
                    __instance.CurrentData.UsedUpgradeHorsesHistory = new List<Tuple<EquipmentElement, int>>();
                    __instance._initialData.CopyFromScreenData(__instance.CurrentData);

                    // In vanilla, the rosters would already be updated but with this patch the rosters are reset on the client to be managed by the server.
                    // This assigns a duplicate version of the left rosters needed in extra logic handled by the PartyScreenHelper when closing the party screen.
                    // For example, the left member roster when creating a new clan party is not managed on the server but the server does need this data.
                    RestoreLeftRostersAfterCommit(
                        __instance,
                        duplicateLeftMemberRoster,
                        duplicateLeftPrisonerRoster);
                }
                finally
                {
                    InCommit = false;
                }
            }
            if (questSelectionRoster != null)
                MessageBroker.Instance.Publish(__instance, new QuestAlternativeTroopSelectionReset(questSelectionRoster, __instance, questSelectionSnapshot));
        }
        __result = flag;
        return false;
    }

    internal static void RestoreLeftRostersAfterCommit(
        PartyScreenLogic partyScreenLogic,
        TroopRoster leftMemberRoster,
        TroopRoster leftPrisonerRoster)
    {
        partyScreenLogic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftMemberRoster;
        partyScreenLogic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftPrisonerRoster;
    }

    [HarmonyPatch(nameof(PartyScreenLogic.OnPartyScreenClosed))]
    [HarmonyPostfix]
    public static void OnPartyScreenClosedPostfix(PartyScreenLogic __instance = null)
    {
        // Cancel skips DoneLogic so the TryClaim there never runs. Drop the
        // attribution here so a cancelled force screen stops gating unrelated
        // party screens. The pool itself stays pending. Post-Done this is a
        // no-op because the claim already cleared the single-shot slot.
        ForceTransferScreenTracker.Clear();
    }

    [HarmonyPatch(nameof(PartyScreenLogic.ExecuteTroop))]
    [HarmonyPostfix]
    public static void ExecuteTroopPostfix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command)
    {
        if (!__instance.ValidateCommand(command)) return;

        // Send message to server to run KillCharacterAction.ApplyByExecution
        var message = new HeroExecuted(command.Character.HeroObject, Hero.MainHero, KillCharacterAction.KillCharacterActionDetail.Executed, false);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(PartyScreenLogic.IsExecutable))]
    [HarmonyPrefix]
    public static bool IsExecutablePrefix(ref bool __result, CharacterObject character)
    {
        if (!HeroExecutionRules.IsExecutable(character.HeroObject, out var _))
        {
            __result = false;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Replace execute button's tooltip for player heroes and companions.
    /// Vanilla doesn't have messages for these because you are not able to capture a player or companion normally.
    /// </summary>
    [HarmonyPatch(nameof(PartyScreenLogic.GetExecutableReasonString))]
    [HarmonyPrefix]
    public static bool GetExecutableReasonStringPrefix(ref string __result, CharacterObject character)
    {
        if (!HeroExecutionRules.IsExecutable(character.HeroObject, out var reason))
        {
            __result = reason;
            return false;
        }

        // Use default message otherwise
        return true;
    }
}
