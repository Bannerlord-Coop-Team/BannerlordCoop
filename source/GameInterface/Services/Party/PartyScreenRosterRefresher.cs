using Common.Logging;
using SandBox.GauntletUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

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

internal readonly struct PartyScreenSelectionIdentity
{
    public CharacterObject Character { get; }
    public PartyScreenLogic.PartyRosterSide Side { get; }
    public PartyScreenLogic.TroopType Type { get; }

    public PartyScreenSelectionIdentity(
        CharacterObject character,
        PartyScreenLogic.PartyRosterSide side,
        PartyScreenLogic.TroopType type)
    {
        Character = character;
        Side = side;
        Type = type;
    }

    public bool Matches(PartyScreenSelectionIdentity other)
        => ReferenceEquals(Character, other.Character) &&
           Side == other.Side &&
           Type == other.Type;
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
    private readonly IPartyScreenLockPreserver lockPreserver;

    public PartyScreenRosterRefresher(
        IPartyScreenRosterBaselineProvider baselineProvider,
        IPartyScreenLockPreserver lockPreserver)
    {
        this.baselineProvider = baselineProvider;
        this.lockPreserver = lockPreserver;
    }

    public bool TryApply(
        TroopRoster authoritativeRoster,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));
        if (character == null) throw new ArgumentNullException(nameof(character));
        if (applyAuthoritative == null) throw new ArgumentNullException(nameof(applyAuthoritative));

        var logic = GetOpenLogic(Game.Current?.GameStateManager);
        return TryApply(
            logic,
            authoritativeRoster,
            character,
            applyAuthoritative,
            NotifyPendingChangesReset);
    }

    /// <summary>
    /// Applies one server troop change under the pending edit, carrying what the edited stack can no longer hold
    /// to the troops moved to the opposite side, and resets the screen when the edit no longer fits.
    /// </summary>
    internal bool TryApply(
        PartyScreenLogic logic,
        TroopRoster authoritativeRoster,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative,
        Action notifyPendingChangesReset)
    {
        var baseline = baselineProvider.GetBaselineRoster(logic, authoritativeRoster);
        if (baseline == null) return false;

        var oppositeBaseline = GetOppositeBaseline(logic, baseline);
        var visible = new ScreenElement(
            GetVisibleRoster(logic, baseline),
            GetVisibleRoster(logic, oppositeBaseline),
            character);
        var saved = new ScreenElement(
            GetSavedRoster(logic, baseline),
            GetSavedRoster(logic, oppositeBaseline),
            character);
        var previousBaseline = RosterElementState.Read(baseline, character);
        var previousOppositeBaseline = RosterElementState.Read(oppositeBaseline, character);

        if (ReferenceEquals(authoritativeRoster, visible.Roster))
        {
            previousBaseline.Write(authoritativeRoster, character);
        }

        applyAuthoritative(authoritativeRoster, character);
        var authoritative = RosterElementState.Read(authoritativeRoster, character);
        bool canRebase = visible.TryRebase(authoritative, previousBaseline, previousOppositeBaseline) &&
            saved.TryRebase(authoritative, previousBaseline, previousOppositeBaseline);

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

        visible.Write();
        saved.Write();
        RefreshRecruitablePrisoners(logic);
        RefreshTroop(
            logic,
            baseline,
            character,
            visible.Previous.Number != visible.Rebased.Number,
            visible.Previous.Wounded != visible.Rebased.Wounded);
        if (visible.OppositeChanged)
            RefreshTroop(logic, oppositeBaseline, character, false, true);
        return true;
    }

    public bool TryRemoveZeroCounts(TroopRoster authoritativeRoster)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));

        var logic = GetOpenLogic(Game.Current?.GameStateManager);
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

        var logic = GetOpenLogic(Game.Current?.GameStateManager);
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

        RefreshUpgrades(logic);
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

    /// <summary>
    /// The screen-open snapshot of the roster that troops from <paramref name="baseline"/> are moved to.
    /// </summary>
    private static TroopRoster GetOppositeBaseline(PartyScreenLogic logic, TroopRoster baseline)
    {
        if (ReferenceEquals(baseline, logic._initialData.RightMemberRoster))
            return logic._initialData.LeftMemberRoster;
        if (ReferenceEquals(baseline, logic._initialData.LeftMemberRoster))
            return logic._initialData.RightMemberRoster;
        if (ReferenceEquals(baseline, logic._initialData.RightPrisonerRoster))
            return logic._initialData.LeftPrisonerRoster;
        return logic._initialData.RightPrisonerRoster;
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

    private void RefreshScreen(PartyScreenLogic logic)
    {
        var partyVm = GetPartyVm(logic);
        var previousSelection = partyVm?.CurrentCharacter;
        bool restoreSelection = previousSelection?.IsSelected == true;
        var selection = restoreSelection
            ? GetIdentity(previousSelection)
            : default;

        lockPreserver.Reset(logic, () => logic.OnReset(false));

        if (!restoreSelection) return;

        var replacement = FindSelectionReplacement(
            partyVm.MainPartyTroops,
            partyVm.MainPartyPrisoners,
            partyVm.OtherPartyTroops,
            partyVm.OtherPartyPrisoners,
            selection,
            GetIdentity);
        if (replacement == null) return;

        partyVm._currentCharacter = replacement;
        partyVm.RefreshCurrentCharacterInformation();
        partyVm.OnPropertyChangedWithValue(replacement, nameof(partyVm.CurrentCharacter));
        replacement.IsSelected = true;
        replacement.UpdateRecruitable();
    }

    private void RefreshTroop(
        PartyScreenLogic logic,
        TroopRoster baseline,
        CharacterObject character,
        bool numberChanged,
        bool woundedChanged)
    {
        var partyVm = GetPartyVm(logic);
        if (partyVm == null) return;

        var identity = GetIdentity(logic, baseline, character);
        var row = FindSelectionReplacement(
            partyVm.MainPartyTroops,
            partyVm.MainPartyPrisoners,
            partyVm.OtherPartyTroops,
            partyVm.OtherPartyPrisoners,
            identity,
            GetIdentity);
        var visible = GetVisibleRoster(logic, baseline);
        int index = visible.FindIndexOfTroop(character);
        if (row == null || index < 0)
        {
            RefreshScreen(logic);
            return;
        }

        row.Troop = visible.GetElementCopyAtIndex(index);
        row.InitializeUpgrades();
        row.UpdateRecruitable();
        row.UpdateTradeData();
        row.ThrowOnPropertyChanged();
        partyVm.UpdateTroopManagerPopUpCounts();
        if (numberChanged)
        {
            partyVm.RefreshPartyInformation();
            partyVm.IsDoneDisabled = !logic.IsDoneActive();
            partyVm.DoneHint.HintText = new TextObject("{=!}" + logic.DoneReasonString);
            partyVm.IsCancelDisabled = !logic.IsCancelActive();
            RefreshSort(logic, partyVm, identity);

            if (identity.Side == PartyScreenLogic.PartyRosterSide.Right)
                partyVm.MainPartyComposition.RefreshCounts(partyVm.MainPartyTroops);
            else
                partyVm.OtherPartyComposition.RefreshCounts(partyVm.OtherPartyTroops);
        }

        if ((numberChanged || woundedChanged) &&
            identity.Side == PartyScreenLogic.PartyRosterSide.Right)
            partyVm.RefreshTopInformation();
    }

    private static void RefreshSort(
        PartyScreenLogic logic,
        TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM partyVm,
        PartyScreenSelectionIdentity identity)
    {
        var sortType = logic.GetActiveSortTypeForSide(identity.Side);
        if (sortType != PartyScreenLogic.TroopSortType.Count) return;

        var troopComparer = logic.GetComparer(sortType);
        troopComparer.SetIsAscending(logic.GetIsAscendingSortForSide(identity.Side));
        var comparer = new TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM.TroopVMComparer(
            troopComparer);
        if (identity.Side == PartyScreenLogic.PartyRosterSide.Right)
        {
            if (identity.Type == PartyScreenLogic.TroopType.Member)
                partyVm.MainPartyTroops.Sort(comparer);
            else
                partyVm.MainPartyPrisoners.Sort(comparer);
        }
        else if (identity.Type == PartyScreenLogic.TroopType.Member)
        {
            partyVm.OtherPartyTroops.Sort(comparer);
        }
        else
        {
            partyVm.OtherPartyPrisoners.Sort(comparer);
        }
    }

    private static void RefreshUpgrades(PartyScreenLogic logic)
    {
        var partyVm = GetPartyVm(logic);
        if (partyVm == null) return;

        foreach (var row in partyVm.MainPartyTroops)
        {
            row.InitializeUpgrades();
        }
        partyVm.UpdateTroopManagerPopUpCounts();
    }

    /// <summary>
    /// The open party screen, also while a conversation started from it is the active state.
    /// </summary>
    internal static PartyScreenLogic GetOpenLogic(GameStateManager gameStateManager)
        => gameStateManager?.LastOrDefault<PartyState>()?.PartyScreenLogic;

    private static TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM GetPartyVm(
        PartyScreenLogic logic)
    {
        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        return partyVm != null && ReferenceEquals(partyVm.PartyScreenLogic, logic)
            ? partyVm
            : null;
    }

    private static PartyScreenSelectionIdentity GetIdentity(
        PartyScreenLogic logic,
        TroopRoster baseline,
        CharacterObject character)
    {
        if (ReferenceEquals(baseline, logic._initialData.RightMemberRoster))
            return new PartyScreenSelectionIdentity(
                character,
                PartyScreenLogic.PartyRosterSide.Right,
                PartyScreenLogic.TroopType.Member);
        if (ReferenceEquals(baseline, logic._initialData.LeftMemberRoster))
            return new PartyScreenSelectionIdentity(
                character,
                PartyScreenLogic.PartyRosterSide.Left,
                PartyScreenLogic.TroopType.Member);
        if (ReferenceEquals(baseline, logic._initialData.RightPrisonerRoster))
            return new PartyScreenSelectionIdentity(
                character,
                PartyScreenLogic.PartyRosterSide.Right,
                PartyScreenLogic.TroopType.Prisoner);
        return new PartyScreenSelectionIdentity(
            character,
            PartyScreenLogic.PartyRosterSide.Left,
            PartyScreenLogic.TroopType.Prisoner);
    }

    private static PartyScreenSelectionIdentity GetIdentity(
        TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyCharacterVM row)
        => new PartyScreenSelectionIdentity(row.Character, row.Side, row.Type);

    internal static T FindSelectionReplacement<T>(
        IEnumerable<T> mainPartyTroops,
        IEnumerable<T> mainPartyPrisoners,
        IEnumerable<T> otherPartyTroops,
        IEnumerable<T> otherPartyPrisoners,
        PartyScreenSelectionIdentity selection,
        Func<T, PartyScreenSelectionIdentity> getIdentity)
        where T : class
    {
        return mainPartyTroops
            .Concat(mainPartyPrisoners)
            .Concat(otherPartyTroops)
            .Concat(otherPartyPrisoners)
            .FirstOrDefault(row => getIdentity(row).Matches(selection));
    }

    private static void NotifyPendingChangesReset()
    {
        Logger.Information(PendingChangesResetMessage);
        MBInformationManager.AddQuickInformation(new TextObject(PendingChangesResetMessage));
    }

    /// <summary>
    /// One troop in a screen roster and in the opposite roster it transfers to, rebased together on a server update.
    /// </summary>
    private sealed class ScreenElement
    {
        private readonly TroopRoster oppositeRoster;
        private readonly CharacterObject character;
        private readonly RosterElementState previousOpposite;
        private RosterElementState rebasedOpposite;

        public TroopRoster Roster { get; }
        public RosterElementState Previous { get; }
        public RosterElementState Rebased { get; private set; }
        public bool OppositeChanged => !rebasedOpposite.Matches(previousOpposite);

        // Reads both rosters before the server change is applied, since the visible roster can be the live one.
        public ScreenElement(TroopRoster roster, TroopRoster oppositeRoster, CharacterObject character)
        {
            Roster = roster;
            this.oppositeRoster = oppositeRoster;
            this.character = character;
            if (roster == null) return;

            Previous = RosterElementState.Read(roster, character);
            previousOpposite = RosterElementState.Read(oppositeRoster, character);
            rebasedOpposite = previousOpposite;
        }

        /// <summary>
        /// Rebases both rosters onto the server's element; a screen without saved popup state has nothing to rebase.
        /// </summary>
        public bool TryRebase(
            RosterElementState authoritative,
            RosterElementState previousBaseline,
            RosterElementState oppositeBaseline)
        {
            if (Roster == null) return true;

            bool rebased = RosterElementState.TryRebase(
                authoritative,
                previousBaseline,
                oppositeBaseline,
                Previous,
                previousOpposite,
                out var rebasedSide,
                out rebasedOpposite);
            Rebased = rebasedSide;
            return rebased;
        }

        /// <summary>
        /// Writes the rebased element, and the opposite one when it took a share of the server change.
        /// </summary>
        public void Write()
        {
            if (Roster == null) return;

            Rebased.Write(Roster, character);
            if (OppositeChanged) rebasedOpposite.Write(oppositeRoster, character);
        }
    }

    /// <summary>
    /// One troop's counts in one roster.
    /// </summary>
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

        /// <summary>
        /// Replays the local edit on the server's element. Wounded and xp this side can no longer hold follow
        /// the troops the edit moved to the opposite side; fails unless a pure transfer accounts for them.
        /// </summary>
        public static bool TryRebase(
            RosterElementState authoritative,
            RosterElementState previousBaseline,
            RosterElementState oppositeBaseline,
            RosterElementState previousSide,
            RosterElementState previousOpposite,
            out RosterElementState rebasedSide,
            out RosterElementState rebasedOpposite)
        {
            rebasedSide = default;
            rebasedOpposite = previousOpposite;
            long removed = (long)previousBaseline.Number - previousSide.Number;
            long number = authoritative.Number - removed;
            if (number < 0 || number > int.MaxValue) return false;

            long wounded = authoritative.Wounded +
                ((long)previousSide.Wounded - previousBaseline.Wounded);
            long keptWounded = Math.Min(number, Math.Max(0L, wounded));
            long movedWounded = wounded - keptWounded;
            // Vanilla TransferTroop carries an emptied stack's xp to the destination, so the server's xp change
            // follows it. A stack the server removed itself carries nothing.
            long movedXp = number == 0 && authoritative.Number > 0
                ? (long)authoritative.Xp - previousBaseline.Xp
                : 0;

            // Only a pure transfer, every troop taken off here landing on the opposite side, tells which troops the
            // change belongs to. An upgrade or recruit in the same edit is ambiguous, so it resets instead.
            bool isPureTransfer = removed > 0 &&
                (long)previousOpposite.Number - oppositeBaseline.Number >= removed;
            if ((movedWounded != 0 || movedXp != 0) && !isPureTransfer) return false;

            long xp = number == 0
                ? 0
                : authoritative.Xp + ((long)previousSide.Xp - previousBaseline.Xp);
            if (xp < 0 || xp > int.MaxValue) return false;
            if (!previousOpposite.TryAbsorb(movedWounded, movedXp, out rebasedOpposite)) return false;

            bool localExistenceChanged = previousSide.Exists != previousBaseline.Exists;
            bool exists = number != 0 ||
                (localExistenceChanged ? previousSide.Exists : authoritative.Exists);
            rebasedSide = new RosterElementState(
                exists,
                (int)number,
                (int)keptWounded,
                (int)xp);
            return true;
        }

        /// <summary>
        /// Adds wounded and xp carried over from the opposite side, failing when this stack cant hold them.
        /// </summary>
        private bool TryAbsorb(long woundedChange, long xpChange, out RosterElementState absorbed)
        {
            absorbed = this;
            if (woundedChange == 0 && xpChange == 0) return true;

            long wounded = Wounded + woundedChange;
            long xp = Xp + xpChange;
            if (Number == 0 || wounded < 0 || wounded > Number || xp < 0 || xp > int.MaxValue) return false;

            absorbed = new RosterElementState(true, Number, (int)wounded, (int)xp);
            return true;
        }

        /// <summary>
        /// Whether both states describe the same element counts.
        /// </summary>
        public bool Matches(RosterElementState other)
            => Exists == other.Exists &&
               Number == other.Number &&
               Wounded == other.Wounded &&
               Xp == other.Xp;

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
