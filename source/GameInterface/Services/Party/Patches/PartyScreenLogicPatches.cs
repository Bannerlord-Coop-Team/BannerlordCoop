using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using MathF = TaleWorlds.Library.MathF;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenLogic))]
internal class PartyScreenLogicPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenLogic>();
    [ThreadStatic]
    private static bool _inCommit;
    internal static bool InCommit
    {
        get => _inCommit;
        private set => _inCommit = value;
    }

    [HarmonyPatch(nameof(PartyScreenLogic.RemoveZeroCounts))]
    [HarmonyPrefix]
    internal static void RemoveZeroCountsPrefix(
        PartyScreenLogic __instance,
        out List<PreservedZeroRow> __state)
    {
        __state = null;
        if (!ModInformation.IsClient) return;

        bool preserveMissingBaseline =
            PartyVMInitializationPatches.IsInitializing(__instance);
        var preservedRows = new List<PreservedZeroRow>();
        CaptureOwnerZeroRows(
            __instance.MemberRosters[0],
            __instance._initialData.LeftMemberRoster,
            __instance.LeftOwnerParty?.MemberRoster,
            preserveMissingBaseline,
            preservedRows);
        CaptureOwnerZeroRows(
            __instance.PrisonerRosters[0],
            __instance._initialData.LeftPrisonerRoster,
            __instance.LeftOwnerParty?.PrisonRoster,
            preserveMissingBaseline,
            preservedRows);
        CaptureOwnerZeroRows(
            __instance.MemberRosters[1],
            __instance._initialData.RightMemberRoster,
            __instance.RightOwnerParty?.MemberRoster,
            preserveMissingBaseline,
            preservedRows);
        CaptureOwnerZeroRows(
            __instance.PrisonerRosters[1],
            __instance._initialData.RightPrisonerRoster,
            __instance.RightOwnerParty?.PrisonRoster,
            preserveMissingBaseline,
            preservedRows);

        if (preservedRows.Count > 0)
        {
            __state = preservedRows;
        }
    }

    [HarmonyPatch(nameof(PartyScreenLogic.RemoveZeroCounts))]
    [HarmonyPostfix]
    internal static void RemoveZeroCountsPostfix(List<PreservedZeroRow> __state)
    {
        if (__state == null) return;

        foreach (var preservedRow in __state)
        {
            RosterElementState.Write(
                preservedRow.Visible,
                preservedRow.Character,
                preservedRow.VisibleState);
            if (preservedRow.SeedBaseline)
            {
                RosterElementState.Write(
                    preservedRow.Baseline,
                    preservedRow.Character,
                    preservedRow.VisibleState);
            }
        }
    }

    internal static void CaptureOwnerZeroRows(
        TroopRoster visible,
        TroopRoster baseline,
        TroopRoster owner,
        bool preserveMissingBaseline,
        List<PreservedZeroRow> preservedRows)
    {
        if (!ReferenceEquals(visible, owner)) return;

        for (int index = 0; index < visible.Count; index++)
        {
            var element = visible.GetElementCopyAtIndex(index);
            if (element.Number != 0) continue;

            var baselineState = RosterElementState.Read(baseline, element.Character);
            if (baselineState.Exists && baselineState.Number != 0) continue;
            if (!baselineState.Exists && !preserveMissingBaseline) continue;

            preservedRows.Add(new PreservedZeroRow(
                visible,
                baseline,
                element.Character,
                RosterElementState.Read(visible, element.Character),
                seedBaseline: !baselineState.Exists));
        }
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
        bool flag = __instance.PartyPresentationDoneButtonDelegate(__instance.MemberRosters[0], __instance.PrisonerRosters[0], __instance.MemberRosters[1], __instance.PrisonerRosters[1], takenPrisonersRoster, releasedPrisonersRoster, isForced, __instance.LeftOwnerParty, __instance.RightOwnerParty);
        bool applyReleasedAndTakenPrisonerActions =
            PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest();
        if (flag)
        {
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
                applyReleasedAndTakenPrisonerActions
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

    /// <summary>
    /// Executing prisoner heroes is disabled in coop: the kill rides KillCharacterAction and its follow-on
    /// death/inheritance handling, which crashes the game when it targets a lord or player
    /// (<see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/2310">issue #2310</see>).
    /// Skipping the original also skips its local prisoner-roster mutation, so nothing diverges from the server.
    /// </summary>
    [HarmonyPatch(nameof(PartyScreenLogic.ExecuteTroop))]
    [HarmonyPrefix]
    public static bool ExecuteTroopPrefix() => false;

    /// <summary>
    /// Reports every prisoner as non-executable so the party screen disables the execute button and
    /// <see cref="PartyScreenLogic.ValidateCommand"/> rejects any ExecuteTroop command (issue #2310).
    /// </summary>
    [HarmonyPatch(nameof(PartyScreenLogic.IsExecutable))]
    [HarmonyPrefix]
    public static bool IsExecutablePrefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    internal const string ExecutionDisabledReason = "Executing prisoners is disabled in Co-op.";

    /// <summary>
    /// The disabled execute button's tooltip; the native "Cannot execute hero right now" would suggest
    /// execution can become available.
    /// </summary>
    [HarmonyPatch(nameof(PartyScreenLogic.GetExecutableReasonString))]
    [HarmonyPrefix]
    public static bool GetExecutableReasonStringPrefix(ref string __result)
    {
        __result = ExecutionDisabledReason;
        return false;
    }

    internal readonly struct PreservedZeroRow
    {
        public readonly TroopRoster Visible;
        public readonly TroopRoster Baseline;
        public readonly CharacterObject Character;
        public readonly RosterElementState VisibleState;
        public readonly bool SeedBaseline;

        public PreservedZeroRow(
            TroopRoster visible,
            TroopRoster baseline,
            CharacterObject character,
            RosterElementState visibleState,
            bool seedBaseline)
        {
            Visible = visible;
            Baseline = baseline;
            Character = character;
            VisibleState = visibleState;
            SeedBaseline = seedBaseline;
        }
    }
}

[HarmonyPatch(typeof(PartyVM), MethodType.Constructor, typeof(PartyScreenLogic))]
internal static class PartyVMInitializationPatches
{
    [ThreadStatic]
    private static PartyScreenLogic _initializingPartyScreenLogic;

    internal static bool IsInitializing(PartyScreenLogic partyScreenLogic) =>
        partyScreenLogic != null &&
        ReferenceEquals(_initializingPartyScreenLogic, partyScreenLogic);

    [HarmonyPrefix]
    internal static void Prefix(
        PartyScreenLogic partyScreenLogic,
        out PartyScreenLogic __state)
    {
        __state = _initializingPartyScreenLogic;
        _initializingPartyScreenLogic = partyScreenLogic;
    }

    [HarmonyFinalizer]
    internal static Exception Finalizer(
        PartyScreenLogic __state,
        Exception __exception)
    {
        _initializingPartyScreenLogic = __state;
        return __exception;
    }
}
