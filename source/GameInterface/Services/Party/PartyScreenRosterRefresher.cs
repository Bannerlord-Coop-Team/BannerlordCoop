using Common.Logging;
using GameInterface.Services.Party.Patches;
using SandBox.GauntletUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
/// Applies received roster changes to an open party screen. The change goes into the screen's baseline, the
/// state the server has, and the player's recorded commands are replayed on top of it, so their edits survive
/// unless a change makes one of them impossible.
/// </summary>
internal class PartyScreenRosterRefresher : IPartyScreenRosterRefresher
{
    private const string CommandsDroppedMessage =
        "Party updated, some pending changes no longer apply:";

    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenRosterRefresher>();
    private readonly IPartyScreenRosterBaselineProvider baselineProvider;
    private readonly IPartyScreenLockPreserver lockPreserver;
    private readonly IPartyScreenCommandTracker commandTracker;

    public PartyScreenRosterRefresher(
        IPartyScreenRosterBaselineProvider baselineProvider,
        IPartyScreenLockPreserver lockPreserver,
        IPartyScreenCommandTracker commandTracker)
    {
        this.baselineProvider = baselineProvider;
        this.lockPreserver = lockPreserver;
        this.commandTracker = commandTracker;
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
        return TryApply(logic, authoritativeRoster, character, applyAuthoritative, NotifyCommandsDropped);
    }

    internal bool TryApply(
        PartyScreenLogic logic,
        TroopRoster authoritativeRoster,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative,
        Action<IReadOnlyList<string>> notifyCommandsDropped)
    {
        var baseline = baselineProvider.GetBaselineRoster(logic, authoritativeRoster);
        if (baseline == null) return false;

        // A roster the screen edits holds the player's edits, so the change goes into the baseline and the replay
        // rebuilds the screen roster. A roster the screen only shows a copy of takes the change directly.
        if (ReferenceEquals(authoritativeRoster, GetVisibleRoster(logic, baseline)))
        {
            applyAuthoritative(baseline, character);
        }
        else
        {
            applyAuthoritative(authoritativeRoster, character);
            RosterElementState.Read(authoritativeRoster, character).Write(baseline, character);
        }

        Replay(logic, notifyCommandsDropped);
        return true;
    }

    public bool TryRemoveZeroCounts(TroopRoster authoritativeRoster)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));

        var logic = GetOpenLogic(Game.Current?.GameStateManager);
        var baseline = baselineProvider.GetBaselineRoster(logic, authoritativeRoster);
        if (baseline == null) return false;

        // Removing empty stacks changes no counts, so the player's edits and the rows stay as they are.
        RemoveZeroCounts(baseline);
        if (!ReferenceEquals(authoritativeRoster, GetVisibleRoster(logic, baseline)))
            RemoveZeroCounts(authoritativeRoster);
        return true;
    }

    public bool TryApply(ItemRoster authoritativeRoster, Action<ItemRoster> applyAuthoritative)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));
        if (applyAuthoritative == null) throw new ArgumentNullException(nameof(applyAuthoritative));

        var logic = GetOpenLogic(Game.Current?.GameStateManager);
        return TryApply(logic, authoritativeRoster, applyAuthoritative, NotifyCommandsDropped);
    }

    internal bool TryApply(
        PartyScreenLogic logic,
        ItemRoster authoritativeRoster,
        Action<ItemRoster> applyAuthoritative,
        Action<IReadOnlyList<string>> notifyCommandsDropped)
    {
        var baseline = baselineProvider.GetBaselineRoster(logic, authoritativeRoster);
        if (baseline == null) return false;

        applyAuthoritative(baseline);
        if (!ReferenceEquals(authoritativeRoster, baseline) &&
            !ReferenceEquals(authoritativeRoster, logic.CurrentData.RightItemRoster))
            applyAuthoritative(authoritativeRoster);

        Replay(logic, notifyCommandsDropped);
        return true;
    }

    private void Replay(PartyScreenLogic logic, Action<IReadOnlyList<string>> notifyCommandsDropped)
    {
        var commands = commandTracker.GetCommands(logic).ToList();
        int savedCount = commandTracker.GetSavedCount(logic);
        var rosters = GetScreenRosters(logic);
        var previousOrder = rosters.Select(GetOrder).ToArray();
        var failed = new List<string>();
        List<string> reduced;

        // The rows are refreshed once afterwards, so the per command UI updates are skipped.
        var updateDelegate = logic.UpdateDelegate;
        logic.UpdateDelegate = null;
        try
        {
            // A command that throws can leave a roster half changed, so start over from the baseline without it.
            while (!ReplayCommands(logic, commands, savedCount, out reduced, out var failedIndex))
            {
                failed.Add($"{commands[failedIndex].Character.Name} x{commands[failedIndex].TotalNumber}: no longer possible");
                commands.RemoveAt(failedIndex);
                if (failedIndex < savedCount) savedCount--;
            }

            logic.RemoveZeroCounts();
            for (int i = 0; i < rosters.Length; i++)
                RestoreOrder(rosters[i], previousOrder[i]);
        }
        finally
        {
            logic.UpdateDelegate = updateDelegate;
        }

        var dropped = failed.Concat(reduced).ToList();
        if (dropped.Count > 0) notifyCommandsDropped(dropped);

        RefreshRows(logic);
    }

    /// <returns>False with the index of the command that threw, true when every command replayed.</returns>
    private bool ReplayCommands(
        PartyScreenLogic logic,
        List<PartyScreenLogic.PartyCommand> commands,
        int savedCount,
        out List<string> reduced,
        out int failedIndex)
    {
        reduced = new List<string>();
        failedIndex = -1;
        logic.CurrentData.ResetUsing(logic._initialData);
        // After the reset, because the recruit model reads the screen's bound prisoner roster, not the baseline.
        RefreshRecruitablePrisoners(logic);
        commandTracker.Clear(logic);
        for (int i = 0; i < commands.Count; i++)
        {
            if (logic._savedData != null && i == savedCount) logic.SavePartyScreenData();

            var command = commands[i];
            int before = PartyScreenCommandRecordingPatches.ReadSource(logic, command).number;
            try
            {
                logic.AddCommand(ClampToAvailable(logic, command));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to replay party screen command {Code} {Character}", command.Code, command.Character?.StringId);
                failedIndex = i;
                return false;
            }

            int replayed = before - PartyScreenCommandRecordingPatches.ReadSource(logic, command).number;
            if (replayed < command.TotalNumber)
            {
                reduced.Add($"{command.Character.Name} x{command.TotalNumber}: " +
                    (replayed <= 0 ? "no longer possible" : $"only {replayed} still possible"));
            }
        }

        if (logic._savedData != null && savedCount >= commands.Count) logic.SavePartyScreenData();
        return true;
    }

    // Moves what is still there when a change took away part of a stack, instead of dropping the whole command.
    // The wounded split is refit too, because healing or injuries change it without changing the count.
    private static PartyScreenLogic.PartyCommand ClampToAvailable(PartyScreenLogic logic, PartyScreenLogic.PartyCommand command)
    {
        bool isTransfer = command.Code == PartyScreenLogic.PartyCommandCode.TransferTroop ||
            command.Code == PartyScreenLogic.PartyCommandCode.TransferTroopToLeaderSlot;
        if (!isTransfer && command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop) return command;

        var source = PartyScreenCommandRecordingPatches.ReadSource(logic, command);
        int number = Math.Min(command.TotalNumber, source.number);
        if (number <= 0) return command;

        var clamped = new PartyScreenLogic.PartyCommand();
        if (isTransfer)
        {
            int wounded = PartyScreenCommandProcessor.ClampWounded(command.WoundedNumber, number, source.number, source.wounded);
            if (number == command.TotalNumber && wounded == command.WoundedNumber) return command;
            clamped.FillForTransferTroop(command.RosterSide, command.Type, command.Character, number, wounded, -1);
        }
        else
        {
            if (number == command.TotalNumber) return command;
            clamped.FillForUpgradeTroop(command.RosterSide, command.Type, command.Character, number, command.UpgradeTarget, -1);
        }

        return clamped;
    }

    private static TroopRoster[] GetScreenRosters(PartyScreenLogic logic) => new[]
    {
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right],
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right],
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left],
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left],
    };

    private static List<CharacterObject> GetOrder(TroopRoster roster)
    {
        var order = new List<CharacterObject>(roster.Count);
        for (int i = 0; i < roster.Count; i++) order.Add(roster.GetCharacterAtIndex(i));
        return order;
    }

    // Replayed transfers land at the end of a roster, so put the stacks back where the player had them.
    private static void RestoreOrder(TroopRoster roster, List<CharacterObject> order)
    {
        int target = 0;
        foreach (var character in order)
        {
            int index = roster.FindIndexOfTroop(character);
            if (index < 0) continue;
            if (index != target) roster.ShiftTroopToIndex(index, target);
            target++;
        }
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

    private static void RemoveZeroCounts(TroopRoster roster)
    {
        roster.RemoveZeroCounts();
        roster.InitializeCachedData();
    }

    private static void RefreshRecruitablePrisoners(PartyScreenLogic logic)
    {
        if (logic.RightOwnerParty?.MobileParty?.IsMainParty != true) return;

        var recruitable = logic._initialData.RightRecruitableData;
        recruitable.Clear();
        foreach (TroopRosterElement element in logic._initialData.RightPrisonerRoster.GetTroopRoster())
        {
            recruitable[element.Character] = Campaign.Current.Models.PrisonerRecruitmentCalculationModel
                .CalculateRecruitableNumber(PartyBase.MainParty, element.Character);
        }
        logic.CurrentData.RightRecruitableData = new Dictionary<CharacterObject, int>(recruitable);
    }

    // Updates the rows in place when the screen still has the same stacks, so scrolling and selection stay put.
    private void RefreshRows(PartyScreenLogic logic)
    {
        var partyVm = GetPartyVm(logic);
        if (partyVm == null) return;

        var rows = new[] { partyVm.MainPartyTroops, partyVm.MainPartyPrisoners, partyVm.OtherPartyTroops, partyVm.OtherPartyPrisoners };
        var rosters = GetScreenRosters(logic);
        for (int i = 0; i < rows.Length; i++)
        {
            if (!RowsMatch(rows[i], rosters[i]))
            {
                RefreshScreen(logic);
                return;
            }
        }

        for (int i = 0; i < rows.Length; i++)
        {
            for (int j = 0; j < rows[i].Count; j++)
            {
                var row = rows[i][j];
                row.Troop = rosters[i].GetElementCopyAtIndex(j);
                row.InitializeUpgrades();
                row.UpdateRecruitable();
                row.UpdateTradeData();
                row.ThrowOnPropertyChanged();
            }
        }

        partyVm.RefreshPartyInformation();
        partyVm.OnPartyGoldChanged();
        partyVm.OnPartyMoraleChanged();
        partyVm.OnPartyHorseChanged();
        partyVm.OnPartyInfluenceChanged();
        partyVm.UpdateTroopManagerPopUpCounts();
        partyVm.MainPartyComposition.RefreshCounts(partyVm.MainPartyTroops);
        partyVm.OtherPartyComposition.RefreshCounts(partyVm.OtherPartyTroops);
        partyVm.RefreshTopInformation();
        partyVm.RefreshCurrentCharacterInformation();
        partyVm.IsDoneDisabled = !logic.IsDoneActive();
        partyVm.DoneHint.HintText = new TextObject("{=!}" + logic.DoneReasonString);
        partyVm.IsCancelDisabled = !logic.IsCancelActive();
    }

    private static bool RowsMatch(MBBindingList<PartyCharacterVM> rows, TroopRoster roster)
    {
        if (rows.Count != roster.Count) return false;
        for (int i = 0; i < rows.Count; i++)
        {
            if (!ReferenceEquals(rows[i].Character, roster.GetCharacterAtIndex(i))) return false;
        }
        return true;
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

    /// <summary>
    /// The open party screen, also while a conversation started from it is the active state.
    /// </summary>
    internal static PartyScreenLogic GetOpenLogic(GameStateManager gameStateManager)
        => gameStateManager?.LastOrDefault<PartyState>()?.PartyScreenLogic;

    private static PartyVM GetPartyVm(PartyScreenLogic logic)
    {
        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        return partyVm != null && ReferenceEquals(partyVm.PartyScreenLogic, logic)
            ? partyVm
            : null;
    }

    private static PartyScreenSelectionIdentity GetIdentity(PartyCharacterVM row)
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

    private static void NotifyCommandsDropped(IReadOnlyList<string> dropped)
    {
        var message = CommandsDroppedMessage + " " + string.Join("; ", dropped);
        Logger.Information(message);
        InformationManager.DisplayMessage(new InformationMessage(message));
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
            return new RosterElementState(true, element.Number, element.WoundedNumber, element.Xp);
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
