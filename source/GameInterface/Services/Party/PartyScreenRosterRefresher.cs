using Common.Logging;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Party;

internal interface IPartyScreenRosterRefresher
{
    bool TryApply(
        TroopRoster authoritativeRoster,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative);

    bool TryRemoveZeroCounts(TroopRoster authoritativeRoster);

    bool TryApply(ItemRoster authoritativeRoster, Action<ItemRoster> applyAuthoritative);
}

/// <summary>
/// Applies received roster changes to an open party screen and its comparison baseline.
/// </summary>
internal class PartyScreenRosterRefresher : IPartyScreenRosterRefresher
{
    private const string PendingChangesResetMessage =
        "Party updated. Pending changes reset.";

    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenRosterRefresher>();
    private readonly IPartyScreenRosterBaselineProvider baselineProvider;

    public PartyScreenRosterRefresher(IPartyScreenRosterBaselineProvider baselineProvider)
    {
        this.baselineProvider = baselineProvider;
    }

    public bool TryApply(
        TroopRoster authoritativeRoster,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));
        if (character == null) throw new ArgumentNullException(nameof(character));
        if (applyAuthoritative == null) throw new ArgumentNullException(nameof(applyAuthoritative));

        var logic = (Game.Current?.GameStateManager?.ActiveState as PartyState)?.PartyScreenLogic;
        return TryApply(
            logic,
            authoritativeRoster,
            character,
            applyAuthoritative,
            NotifyPendingChangesReset);
    }

    internal bool TryApply(
        PartyScreenLogic logic,
        TroopRoster authoritativeRoster,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative,
        Action notifyPendingChangesReset)
    {
        var baseline = baselineProvider.GetBaselineRoster(logic, authoritativeRoster);
        if (baseline == null) return false;

        var visible = GetVisibleRoster(logic, baseline);
        var saved = GetSavedRoster(logic, baseline);
        var previousBaseline = RosterElementState.Read(baseline, character);
        var previousVisible = RosterElementState.Read(visible, character);
        var previousSaved = saved == null
            ? default
            : RosterElementState.Read(saved, character);

        if (ReferenceEquals(authoritativeRoster, visible))
        {
            previousBaseline.Write(authoritativeRoster, character);
        }

        applyAuthoritative(authoritativeRoster, character);
        var authoritative = RosterElementState.Read(authoritativeRoster, character);

        RosterElementState rebasedSaved = default;
        bool canRebase =
            RosterElementState.TryRebase(authoritative, previousBaseline, previousVisible, out var rebasedVisible) &&
            (saved == null ||
                RosterElementState.TryRebase(
                    authoritative,
                    previousBaseline,
                    previousSaved,
                    out rebasedSaved));

        authoritative.Write(baseline, character);
        if (!canRebase)
        {
            logic.CurrentData.ResetUsing(logic._initialData);
            RefreshRecruitablePrisoners(logic);
            if (logic._savedData != null) logic.SavePartyScreenData();
            RefreshScreen(logic);
            notifyPendingChangesReset();
            return true;
        }

        rebasedVisible.Write(visible, character);
        if (saved != null) rebasedSaved.Write(saved, character);
        RefreshRecruitablePrisoners(logic);
        RefreshScreen(logic);
        return true;
    }

    public bool TryRemoveZeroCounts(TroopRoster authoritativeRoster)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));

        var logic = (Game.Current?.GameStateManager?.ActiveState as PartyState)?.PartyScreenLogic;
        var baseline = baselineProvider.GetBaselineRoster(logic, authoritativeRoster);
        if (baseline == null) return false;

        var visible = GetVisibleRoster(logic, baseline);
        var saved = GetSavedRoster(logic, baseline);
        RemoveZeroCounts(authoritativeRoster);
        if (!ReferenceEquals(authoritativeRoster, baseline)) RemoveZeroCounts(baseline);
        if (!ReferenceEquals(authoritativeRoster, visible) &&
            !ReferenceEquals(baseline, visible))
        {
            RemoveZeroCounts(visible);
        }
        if (saved != null &&
            !ReferenceEquals(authoritativeRoster, saved) &&
            !ReferenceEquals(baseline, saved) &&
            !ReferenceEquals(visible, saved))
        {
            RemoveZeroCounts(saved);
        }

        RefreshRecruitablePrisoners(logic);
        RefreshScreen(logic);
        return true;
    }

    public bool TryApply(ItemRoster authoritativeRoster, Action<ItemRoster> applyAuthoritative)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));
        if (applyAuthoritative == null) throw new ArgumentNullException(nameof(applyAuthoritative));

        var logic = (Game.Current?.GameStateManager?.ActiveState as PartyState)?.PartyScreenLogic;
        return TryApply(logic, authoritativeRoster, applyAuthoritative);
    }

    internal bool TryApply(
        PartyScreenLogic logic,
        ItemRoster authoritativeRoster,
        Action<ItemRoster> applyAuthoritative)
    {
        var baseline = baselineProvider.GetBaselineRoster(logic, authoritativeRoster);
        if (baseline == null) return false;

        var visible = logic.CurrentData.RightItemRoster;
        var saved = logic._savedData?.RightItemRoster;
        applyAuthoritative(authoritativeRoster);
        if (!ReferenceEquals(authoritativeRoster, baseline)) applyAuthoritative(baseline);
        if (visible != null &&
            !ReferenceEquals(authoritativeRoster, visible) &&
            !ReferenceEquals(baseline, visible))
        {
            applyAuthoritative(visible);
        }
        if (saved != null &&
            !ReferenceEquals(authoritativeRoster, saved) &&
            !ReferenceEquals(baseline, saved) &&
            !ReferenceEquals(visible, saved))
        {
            applyAuthoritative(saved);
        }

        RefreshScreen(logic);
        return true;
    }

    private static TroopRoster GetVisibleRoster(PartyScreenLogic logic, TroopRoster baseline)
    {
        if (ReferenceEquals(baseline, logic._initialData.RightMemberRoster))
            return logic.CurrentData.RightMemberRoster;
        if (ReferenceEquals(baseline, logic._initialData.LeftMemberRoster))
            return logic.CurrentData.LeftMemberRoster;
        if (ReferenceEquals(baseline, logic._initialData.RightPrisonerRoster))
            return logic.CurrentData.RightPrisonerRoster;
        return logic.CurrentData.LeftPrisonerRoster;
    }

    private static TroopRoster GetSavedRoster(PartyScreenLogic logic, TroopRoster baseline)
    {
        if (logic._savedData == null) return null;
        if (ReferenceEquals(baseline, logic._initialData.RightMemberRoster))
            return logic._savedData.RightMemberRoster;
        if (ReferenceEquals(baseline, logic._initialData.LeftMemberRoster))
            return logic._savedData.LeftMemberRoster;
        if (ReferenceEquals(baseline, logic._initialData.RightPrisonerRoster))
            return logic._savedData.RightPrisonerRoster;
        return logic._savedData.LeftPrisonerRoster;
    }

    private static void RemoveZeroCounts(TroopRoster roster)
    {
        roster.RemoveZeroCounts();
        roster.InitializeCachedData();
    }

    private static void RefreshRecruitablePrisoners(PartyScreenLogic logic)
    {
        if (logic.RightOwnerParty?.MobileParty?.IsMainParty != true) return;

        RefreshRecruitablePrisoners(
            logic._initialData.RightPrisonerRoster,
            logic._initialData.RightRecruitableData);
        if (logic._savedData != null)
        {
            RefreshRecruitablePrisoners(
                logic._savedData.RightPrisonerRoster,
                logic._savedData.RightRecruitableData);
        }
    }

    private static void RefreshRecruitablePrisoners(
        TroopRoster prisonerRoster,
        System.Collections.Generic.Dictionary<CharacterObject, int> recruitable)
    {
        recruitable.Clear();
        foreach (TroopRosterElement element in prisonerRoster.GetTroopRoster())
        {
            recruitable[element.Character] = Campaign.Current.Models.PrisonerRecruitmentCalculationModel
                .CalculateRecruitableNumber(PartyBase.MainParty, element.Character);
        }
    }

    private static void RefreshScreen(PartyScreenLogic logic)
    {
        logic.OnReset(false);
    }

    private static void NotifyPendingChangesReset()
    {
        Logger.Information(PendingChangesResetMessage);
        MBInformationManager.AddQuickInformation(new TextObject(PendingChangesResetMessage));
    }

    private readonly struct RosterElementState
    {
        public bool Exists { get; }
        public int Number { get; }
        public int Wounded { get; }
        public int Xp { get; }

        private RosterElementState(bool exists, int number, int wounded, int xp)
        {
            Exists = exists;
            Number = number;
            Wounded = wounded;
            Xp = xp;
        }

        public static RosterElementState Read(TroopRoster roster, CharacterObject character)
        {
            int index = roster.FindIndexOfTroop(character);
            if (index < 0) return default;

            var element = roster.GetElementCopyAtIndex(index);
            return new RosterElementState(
                true,
                element.Number,
                element.WoundedNumber,
                element.Xp);
        }

        public static bool TryRebase(
            RosterElementState authoritative,
            RosterElementState previousBaseline,
            RosterElementState previousVisible,
            out RosterElementState rebased)
        {
            long number = authoritative.Number +
                ((long)previousVisible.Number - previousBaseline.Number);
            long wounded = authoritative.Wounded +
                ((long)previousVisible.Wounded - previousBaseline.Wounded);
            long xp = authoritative.Xp +
                ((long)previousVisible.Xp - previousBaseline.Xp);
            if (number < 0 ||
                number > int.MaxValue ||
                wounded < 0 ||
                wounded > number ||
                xp < 0 ||
                xp > int.MaxValue ||
                (number == 0 && xp != 0))
            {
                rebased = default;
                return false;
            }

            bool localExistenceChanged = previousVisible.Exists != previousBaseline.Exists;
            bool exists = number != 0 ||
                wounded != 0 ||
                xp != 0 ||
                (localExistenceChanged ? previousVisible.Exists : authoritative.Exists);
            rebased = new RosterElementState(
                exists,
                (int)number,
                (int)wounded,
                (int)xp);
            return true;
        }

        public void Write(TroopRoster roster, CharacterObject character)
        {
            int index = roster.FindIndexOfTroop(character);
            if (!Exists)
            {
                if (index >= 0)
                {
                    roster.RemoveIf(element => ReferenceEquals(element.Character, character));
                    roster.InitializeCachedData();
                }
                return;
            }

            if (index < 0) index = roster.AddNewElement(character, -1);
            roster.SetElementNumber(index, Number);
            roster.SetElementWoundedNumber(index, Wounded);
            roster.SetElementXp(index, Xp);
            roster.InitializeCachedData();
        }
    }
}
