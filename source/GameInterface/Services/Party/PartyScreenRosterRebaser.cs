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
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Party;

internal interface IPartyScreenRosterRebaser
{
    bool TryApply(
        TroopRoster authoritativeRoster,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative);

    bool TryRemoveZeroCounts(TroopRoster authoritativeRoster);
}

/// <summary>
/// Rebases an open party screen over received authoritative roster changes while retaining compatible
/// unsent player edits.
/// </summary>
internal class PartyScreenRosterRebaser : IPartyScreenRosterRebaser
{
    private const string ConflictMessage =
        "Your party changed on the server. Pending party-screen changes were reset.";

    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenRosterRebaser>();
    private readonly IPartyScreenRosterBaselineProvider baselineProvider;

    public PartyScreenRosterRebaser(
        IPartyScreenRosterBaselineProvider baselineProvider)
    {
        if (baselineProvider == null)
            throw new ArgumentNullException(nameof(baselineProvider));

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

        if (!TryGetContext(authoritativeRoster, out var context)) return false;

        bool visibleAliasesAuthoritative = ReferenceEquals(context.Visible, authoritativeRoster);
        var authoritativeRosterBefore = RosterElementState.Read(authoritativeRoster, character);
        var baselineBefore = RosterElementState.SeedOmittedZeroRow(
            RosterElementState.Read(context.Baseline, character),
            authoritativeRosterBefore,
            copyXp: visibleAliasesAuthoritative);
        var visibleBefore = RosterElementState.SeedOmittedZeroRow(
            RosterElementState.Read(context.Visible, character),
            baselineBefore,
            copyXp: true);

        var canonicalBefore = visibleAliasesAuthoritative
            ? baselineBefore
            : authoritativeRosterBefore;
        var savedBefore = context.Saved == null
            ? default
            : RosterElementState.SeedOmittedZeroRow(
                RosterElementState.Read(context.Saved, character),
                baselineBefore,
                copyXp: true);

        // Detached screens can omit owner XP, so replay the operation against both baselines.
        if (!TryApplyToScratch(baselineBefore, character, applyAuthoritative, out var baselineAfter))
        {
            ResetPendingChangesAndApply(
                context,
                authoritativeRoster,
                character,
                applyAuthoritative,
                baselineBefore,
                canonicalBefore);
            return true;
        }

        var canonicalAfter = baselineAfter;
        if ((!visibleAliasesAuthoritative &&
             !TryApplyToScratch(canonicalBefore, character, applyAuthoritative, out canonicalAfter)) ||
            !TryApplyToScratch(visibleBefore, character, applyAuthoritative, out var visibleAfterOperation) ||
            !RosterElementState.TryRebase(
                baselineBefore,
                visibleBefore,
                baselineAfter,
                visibleAfterOperation.Exists,
                out var rebasedVisible))
        {
            ResetPendingChangesAndApply(
                context,
                authoritativeRoster,
                character,
                applyAuthoritative,
                baselineBefore,
                canonicalBefore);
            return true;
        }

        RosterElementState rebasedSaved = default;
        if (context.Saved != null &&
            (!TryApplyToScratch(savedBefore, character, applyAuthoritative, out var savedAfterOperation) ||
             !RosterElementState.TryRebase(
                 baselineBefore,
                 savedBefore,
                 baselineAfter,
                 savedAfterOperation.Exists,
                 out rebasedSaved)))
        {
            ResetPendingChangesAndApply(
                context,
                authoritativeRoster,
                character,
                applyAuthoritative,
                baselineBefore,
                canonicalBefore);
            return true;
        }

        RecruitableState authoritativeRecruitable = default;
        RecruitableState rebasedVisibleRecruitable = default;
        RecruitableState rebasedSavedRecruitable = default;
        if (context.TracksRecruitability)
        {
            var visibleRecruitableBefore = RecruitableState.Read(
                context.Logic.CurrentData.RightRecruitableData,
                character);
            var savedRecruitableBefore = context.Saved == null
                ? default
                : RecruitableState.Read(
                    context.Logic._savedData.RightRecruitableData,
                    character);

            authoritativeRecruitable = CalculateRecruitable(
                context,
                character,
                canonicalAfter,
                hasKey: canonicalAfter.Exists);
            var authoritativeCapacityBefore = CalculateRecruitable(
                context,
                character,
                canonicalBefore,
                hasKey: true);
            var authoritativeCapacityAfter = CalculateRecruitable(
                context,
                character,
                canonicalAfter,
                hasKey: true);
            var visibleCapacityBefore = visibleAliasesAuthoritative
                ? CalculateRecruitable(context, character, visibleBefore, hasKey: true)
                : authoritativeCapacityBefore;
            var visibleCapacityAfter = visibleAliasesAuthoritative
                ? CalculateRecruitable(context, character, rebasedVisible, hasKey: true)
                : authoritativeCapacityAfter;

            if (!RecruitableState.TryRebase(
                    visibleRecruitableBefore,
                    visibleCapacityBefore.Value,
                    visibleCapacityAfter.Value,
                    visibleRecruitableBefore.Exists || authoritativeRecruitable.Exists,
                    out rebasedVisibleRecruitable))
            {
                ResetPendingChangesAndApply(
                    context,
                    authoritativeRoster,
                    character,
                    applyAuthoritative,
                    baselineBefore,
                    canonicalBefore);
                return true;
            }

            if (context.Saved != null)
            {
                var savedCapacityBefore = visibleAliasesAuthoritative
                    ? CalculateRecruitable(context, character, savedBefore, hasKey: true)
                    : authoritativeCapacityBefore;
                var savedCapacityAfter = visibleAliasesAuthoritative
                    ? CalculateRecruitable(context, character, rebasedSaved, hasKey: true)
                    : authoritativeCapacityAfter;
                if (!RecruitableState.TryRebase(
                        savedRecruitableBefore,
                        savedCapacityBefore.Value,
                        savedCapacityAfter.Value,
                        savedRecruitableBefore.Exists || authoritativeRecruitable.Exists,
                        out rebasedSavedRecruitable))
                {
                    ResetPendingChangesAndApply(
                        context,
                        authoritativeRoster,
                        character,
                        applyAuthoritative,
                        baselineBefore,
                        canonicalBefore);
                    return true;
                }
            }
        }

        try
        {
            applyAuthoritative(authoritativeRoster, character);
            RosterElementState.Write(context.Baseline, character, baselineAfter);
            RosterElementState.Write(context.Visible, character, rebasedVisible);
            if (context.Saved != null)
            {
                RosterElementState.Write(context.Saved, character, rebasedSaved);
            }

            if (context.TracksRecruitability)
            {
                RecruitableState.Write(
                    context.Logic._initialData.RightRecruitableData,
                    character,
                    authoritativeRecruitable);
                RecruitableState.Write(
                    context.Logic.CurrentData.RightRecruitableData,
                    character,
                    rebasedVisibleRecruitable);
                if (context.Saved != null)
                {
                    RecruitableState.Write(
                        context.Logic._savedData.RightRecruitableData,
                        character,
                        rebasedSavedRecruitable);
                }
            }

            RefreshVisibleRoster(context, character);
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Failed to apply rebased authoritative party-screen roster update for {Character}",
                character.StringId);
            ResetPendingChanges(context);
        }

        return true;
    }

    public bool TryRemoveZeroCounts(TroopRoster authoritativeRoster)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));
        if (!TryGetContext(authoritativeRoster, out var context)) return false;

        authoritativeRoster.RemoveZeroCounts();
        authoritativeRoster.InitializeCachedData();

        if (!ReferenceEquals(authoritativeRoster, context.Baseline))
        {
            context.Baseline.RemoveZeroCounts();
            context.Baseline.InitializeCachedData();
        }
        if (!ReferenceEquals(authoritativeRoster, context.Visible))
        {
            context.Visible.RemoveZeroCounts();
            context.Visible.InitializeCachedData();
        }
        if (context.Saved != null &&
            !ReferenceEquals(authoritativeRoster, context.Saved))
        {
            context.Saved.RemoveZeroCounts();
            context.Saved.InitializeCachedData();
        }

        RefreshVisibleRoster(context, character: null, forceListRebuild: true);
        return true;
    }

    private static bool TryApplyToScratch(
        RosterElementState baseline,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative,
        out RosterElementState authoritativeAfter)
    {
        var scratch = TroopRoster.CreateDummyTroopRoster();
        RosterElementState.Write(scratch, character, baseline);

        try
        {
            applyAuthoritative(scratch, character);
            authoritativeAfter = RosterElementState.Read(scratch, character);
            return authoritativeAfter.IsValid;
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Failed to rebase authoritative party-screen roster update for {Character}",
                character.StringId);
            authoritativeAfter = baseline;
            return false;
        }
    }

    private static void ResetPendingChangesAndApply(
        ScreenRosterContext context,
        TroopRoster authoritativeRoster,
        CharacterObject character,
        Action<TroopRoster, CharacterObject> applyAuthoritative,
        RosterElementState baselineBefore,
        RosterElementState canonicalBefore)
    {
        context.Logic.Reset(false);

        try
        {
            RosterElementState.Write(authoritativeRoster, character, canonicalBefore);
            applyAuthoritative(authoritativeRoster, character);
            if (!ReferenceEquals(authoritativeRoster, context.Baseline))
            {
                RosterElementState.Write(context.Baseline, character, baselineBefore);
                applyAuthoritative(context.Baseline, character);
            }
            if (!ReferenceEquals(authoritativeRoster, context.Visible) &&
                !ReferenceEquals(context.Baseline, context.Visible))
            {
                RosterElementState.Write(context.Visible, character, baselineBefore);
                applyAuthoritative(context.Visible, character);
            }

            if (context.TracksRecruitability)
            {
                var canonicalAfter = RosterElementState.Read(authoritativeRoster, character);
                var authoritativeRecruitable = CalculateRecruitable(
                    context,
                    character,
                    canonicalAfter,
                    hasKey: canonicalAfter.Exists);
                RecruitableState.Write(
                    context.Logic._initialData.RightRecruitableData,
                    character,
                    authoritativeRecruitable);
                RecruitableState.Write(
                    context.Logic.CurrentData.RightRecruitableData,
                    character,
                    authoritativeRecruitable);
            }

            if (context.Saved != null)
            {
                context.Logic.SavePartyScreenData();
            }

            RefreshVisibleRoster(context, character);
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Failed to apply authoritative roster update after resetting party-screen changes for {Character}",
                character.StringId);
        }

        NotifyConflict();
    }

    private static void ResetPendingChanges(ScreenRosterContext context)
    {
        context.Logic.Reset(false);
        if (context.Saved != null)
        {
            context.Logic.SavePartyScreenData();
        }
        NotifyConflict();
    }

    private static RecruitableState CalculateRecruitable(
        ScreenRosterContext context,
        CharacterObject character,
        RosterElementState authoritativeState,
        bool hasKey)
    {
        if (!context.TracksRecruitability || !hasKey)
            return default;

        // CalculateRecruitableNumber reads the locally edited live roster, so derive the same value
        // from the matching rebased element state instead.
        int conformityNeeded = character.IsHero
            ? 0
            : Campaign.Current.Models.PrisonerRecruitmentCalculationModel
                .GetConformityNeededToRecruitPrisoner(character);
        return RecruitableState.FromRosterState(
            authoritativeState,
            character.IsHero,
            conformityNeeded,
            hasKey);
    }

    private static void NotifyConflict()
    {
        Logger.Warning(ConflictMessage);
        MBInformationManager.AddQuickInformation(new TextObject(ConflictMessage));
    }

    private bool TryGetContext(TroopRoster authoritativeRoster, out ScreenRosterContext context)
    {
        context = default;
        var partyState = Game.Current?.GameStateManager?.LastOrDefault<PartyState>();
        if (partyState == null) return false;

        var logic = partyState.PartyScreenLogic;
        if (logic == null) return false;
        var savedData = IsPartyPopupOpen(logic) ? logic._savedData : null;

        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = (PartyScreenLogic.PartyRosterSide)sideIndex;
            var owner = side == PartyScreenLogic.PartyRosterSide.Left
                ? logic.LeftOwnerParty
                : logic.RightOwnerParty;

            var visibleMembers = logic.MemberRosters[sideIndex];
            var baselineMembers = baselineProvider.GetBaselineRoster(visibleMembers);
            var savedMembers = savedData == null
                ? null
                : side == PartyScreenLogic.PartyRosterSide.Left
                    ? savedData.LeftMemberRoster
                    : savedData.RightMemberRoster;
            if (baselineMembers != null &&
                RepresentsAuthoritativeRoster(
                    authoritativeRoster,
                    visibleMembers,
                    baselineMembers,
                    owner?.MemberRoster,
                    logic.MemberTransferState))
            {
                context = new ScreenRosterContext(
                    logic,
                    visibleMembers,
                    baselineMembers,
                    savedMembers,
                    side,
                    PartyScreenLogic.TroopType.Member);
                return true;
            }

            var visiblePrisoners = logic.PrisonerRosters[sideIndex];
            var baselinePrisoners = baselineProvider.GetBaselineRoster(visiblePrisoners);
            var savedPrisoners = savedData == null
                ? null
                : side == PartyScreenLogic.PartyRosterSide.Left
                    ? savedData.LeftPrisonerRoster
                    : savedData.RightPrisonerRoster;
            if (baselinePrisoners != null &&
                RepresentsAuthoritativeRoster(
                    authoritativeRoster,
                    visiblePrisoners,
                    baselinePrisoners,
                    owner?.PrisonRoster,
                    logic.PrisonerTransferState))
            {
                context = new ScreenRosterContext(
                    logic,
                    visiblePrisoners,
                    baselinePrisoners,
                    savedPrisoners,
                    side,
                    PartyScreenLogic.TroopType.Prisoner);
                return true;
            }
        }

        return false;
    }

    private static bool RepresentsAuthoritativeRoster(
        TroopRoster authoritative,
        TroopRoster visible,
        TroopRoster baseline,
        TroopRoster ownerRoster,
        PartyScreenLogic.TransferState transferState)
    {
        if (ReferenceEquals(authoritative, visible)) return true;
        if (!ReferenceEquals(authoritative, ownerRoster)) return false;
        if (!RosterElementState.HasPopulatedRows(baseline) &&
            !RosterElementState.HasPopulatedRows(authoritative))
        {
            return transferState != PartyScreenLogic.TransferState.NotTransferable;
        }

        // CloneRosterData drops XP and retained zero rows, so match the populated composition.
        return RosterElementState.RostersMatchOwnerSnapshot(baseline, authoritative);
    }

    private static bool IsPartyPopupOpen(PartyScreenLogic logic)
    {
        if (!(ScreenManager.TopScreen is GauntletPartyScreen partyScreen)) return false;

        var partyVm = partyScreen._dataSource;
        return partyVm != null &&
               ReferenceEquals(partyVm.PartyScreenLogic, logic) &&
               partyVm.IsAnyPopUpOpen;
    }

    private static void RefreshVisibleRoster(
        ScreenRosterContext context,
        CharacterObject character,
        bool forceListRebuild = false)
    {
        if (!(ScreenManager.TopScreen is GauntletPartyScreen partyScreen)) return;

        var partyVm = partyScreen._dataSource;
        if (partyVm == null || !ReferenceEquals(partyVm.PartyScreenLogic, context.Logic)) return;

        var list = GetList(partyVm, context.Side, context.Type);
        var index = character == null ? -1 : context.Visible.FindIndexOfTroop(character);
        var existingVm = character == null
            ? null
            : list.FirstOrDefault(vm => vm.Character == character);

        if (!forceListRebuild && index >= 0 && existingVm != null)
        {
            existingVm.Troop = context.Visible.GetElementCopyAtIndex(index);
            existingVm.Index = index;
            existingVm.InitializeUpgrades();
            existingVm.UpdateRecruitable();
            existingVm.UpdateTradeData();
            existingVm.ThrowOnPropertyChanged();
        }
        else
        {
            var selectedCharacter = partyVm.CurrentCharacter;
            partyVm.InitializePartyList(list, context.Side, context.Type);
            RestoreSort(partyVm, context);
            RestoreSelection(partyVm, selectedCharacter);
        }

        partyVm.RefreshPartyInformation();
        partyVm.RefreshTopInformation();
        partyVm.RefreshTroopsUpgradeable();
        partyVm.RefreshPrisonersRecruitable();
        RefreshOpenPopup(partyVm, context);
        partyVm.UpdateTroopManagerPopUpCounts();
        partyVm.MainPartyComposition.RefreshCounts(partyVm.MainPartyTroops);
        partyVm.OtherPartyComposition.RefreshCounts(partyVm.OtherPartyTroops);
        partyVm.IsDoneDisabled = !context.Logic.IsDoneActive();
        partyVm.DoneHint.HintText = new TextObject("{=!}" + context.Logic.DoneReasonString);
        if (partyVm.CurrentCharacter != null)
        {
            partyVm.RefreshCurrentCharacterInformation();
        }
    }

    private static void RefreshOpenPopup(PartyVM partyVm, ScreenRosterContext context)
    {
        if (context.Side != PartyScreenLogic.PartyRosterSide.Right) return;

        if (context.Type == PartyScreenLogic.TroopType.Member &&
            partyVm.UpgradePopUp?.IsOpen == true)
        {
            var focusedCharacter = partyVm.UpgradePopUp.FocusedTroop?.PartyCharacter?.Character;
            partyVm.UpgradePopUp.PopulateTroops();
            partyVm.UpgradePopUp.UpdateUpgradesOfAllTroops();
            partyVm.UpgradePopUp.SetFocusedCharacter(
                partyVm.UpgradePopUp.Troops.FirstOrDefault(
                    item => item.PartyCharacter.Character == focusedCharacter));
            partyVm.UpgradePopUp.UpdateLabels();
            return;
        }

        if (context.Type == PartyScreenLogic.TroopType.Prisoner &&
            partyVm.RecruitPopUp?.IsOpen == true)
        {
            var focusedCharacter = partyVm.RecruitPopUp.FocusedTroop?.PartyCharacter?.Character;
            partyVm.RecruitPopUp.PopulateTroops();
            partyVm.RecruitPopUp.SetFocusedCharacter(
                partyVm.RecruitPopUp.Troops.FirstOrDefault(
                    item => item.PartyCharacter.Character == focusedCharacter));
            partyVm.RecruitPopUp.UpdateLabels();
        }
    }

    private static MBBindingList<PartyCharacterVM> GetList(
        PartyVM partyVm,
        PartyScreenLogic.PartyRosterSide side,
        PartyScreenLogic.TroopType type)
    {
        if (side == PartyScreenLogic.PartyRosterSide.Left)
        {
            return type == PartyScreenLogic.TroopType.Member
                ? partyVm.OtherPartyTroops
                : partyVm.OtherPartyPrisoners;
        }

        return type == PartyScreenLogic.TroopType.Member
            ? partyVm.MainPartyTroops
            : partyVm.MainPartyPrisoners;
    }

    private static void RestoreSort(PartyVM partyVm, ScreenRosterContext context)
    {
        if (context.Side == PartyScreenLogic.PartyRosterSide.Left)
        {
            partyVm.OtherPartySortController?.SortWith(
                context.Logic.ActiveOtherPartySortType,
                context.Logic.IsOtherPartySortAscending);
            return;
        }

        partyVm.MainPartySortController?.SortWith(
            context.Logic.ActiveMainPartySortType,
            context.Logic.IsMainPartySortAscending);
    }

    private static void RestoreSelection(PartyVM partyVm, PartyCharacterVM previousSelection)
    {
        var characters = partyVm.MainPartyTroops
            .Concat(partyVm.MainPartyPrisoners)
            .Concat(partyVm.OtherPartyTroops)
            .Concat(partyVm.OtherPartyPrisoners);
        var selected = previousSelection == null
            ? null
            : characters.FirstOrDefault(vm =>
                vm.Character == previousSelection.Character &&
                vm.Side == previousSelection.Side &&
                vm.Type == previousSelection.Type);
        selected ??= partyVm.MainPartyTroops.FirstOrDefault()
            ?? partyVm.OtherPartyTroops.FirstOrDefault()
            ?? partyVm.MainPartyPrisoners.FirstOrDefault()
            ?? partyVm.OtherPartyPrisoners.FirstOrDefault();
        if (selected != null)
        {
            partyVm.SetSelectedCharacter(selected);
            selected.IsSelected = previousSelection?.IsSelected == true;
        }
    }

    private readonly struct ScreenRosterContext
    {
        public readonly PartyScreenLogic Logic;
        public readonly TroopRoster Visible;
        public readonly TroopRoster Baseline;
        public readonly TroopRoster Saved;
        public readonly PartyScreenLogic.PartyRosterSide Side;
        public readonly PartyScreenLogic.TroopType Type;

        public bool TracksRecruitability =>
            Side == PartyScreenLogic.PartyRosterSide.Right &&
            Type == PartyScreenLogic.TroopType.Prisoner &&
            Logic.RightOwnerParty?.MobileParty?.IsMainParty == true;

        public ScreenRosterContext(
            PartyScreenLogic logic,
            TroopRoster visible,
            TroopRoster baseline,
            TroopRoster saved,
            PartyScreenLogic.PartyRosterSide side,
            PartyScreenLogic.TroopType type)
        {
            Logic = logic;
            Visible = visible;
            Baseline = baseline;
            Saved = saved;
            Side = side;
            Type = type;
        }
    }
}

internal readonly struct RosterElementState
{
    public readonly bool Exists;
    public readonly int Number;
    public readonly int Wounded;
    public readonly int Xp;

    public bool IsValid =>
        Number >= 0 &&
        Wounded >= 0 &&
        Wounded <= Number &&
        Xp >= 0 &&
        (Exists || (Number == 0 && Wounded == 0 && Xp == 0));

    public RosterElementState(int number, int wounded, int xp)
        : this(number, wounded, xp, exists: true)
    {
    }

    public RosterElementState(int number, int wounded, int xp, bool exists)
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
            element.Number,
            element.WoundedNumber,
            element.Xp,
            exists: true);
    }

    public static void Write(TroopRoster roster, CharacterObject character, RosterElementState target)
    {
        if (!target.IsValid)
            throw new ArgumentOutOfRangeException(nameof(target));

        var current = Read(roster, character);
        if (current.Exists == target.Exists &&
            current.Number == target.Number &&
            current.Wounded == target.Wounded &&
            current.Xp == target.Xp)
        {
            return;
        }

        int index = roster.FindIndexOfTroop(character);
        if (!target.Exists)
        {
            if (index >= 0)
            {
                roster.RemoveRange(index, index + 1);
                roster.InitializeCachedData();
            }
            return;
        }

        if (index < 0)
        {
            index = roster.AddNewElement(character, -1);
        }

        roster.data[index].Number = target.Number;
        roster.data[index].WoundedNumber = target.Wounded;
        roster.data[index].Xp = target.Xp;
        roster.InitializeCachedData();
        roster.UpdateVersion();
    }

    public static bool TryRebase(
        RosterElementState baselineBefore,
        RosterElementState visibleBefore,
        RosterElementState authoritativeAfter,
        bool existsAfterApplyingAuthoritativeToVisible,
        out RosterElementState rebased)
    {
        long number = (long)authoritativeAfter.Number + visibleBefore.Number - baselineBefore.Number;
        long wounded = (long)authoritativeAfter.Wounded + visibleBefore.Wounded - baselineBefore.Wounded;
        long xp = (long)authoritativeAfter.Xp + visibleBefore.Xp - baselineBefore.Xp;

        if (number < int.MinValue || number > int.MaxValue ||
            wounded < int.MinValue || wounded > int.MaxValue ||
            xp < int.MinValue || xp > int.MaxValue)
        {
            rebased = default;
            return false;
        }

        bool exists =
            number != 0 ||
            wounded != 0 ||
            xp != 0 ||
            existsAfterApplyingAuthoritativeToVisible;
        rebased = new RosterElementState((int)number, (int)wounded, (int)xp, exists);
        return rebased.IsValid;
    }

    public static RosterElementState SeedOmittedZeroRow(
        RosterElementState screen,
        RosterElementState source,
        bool copyXp)
    {
        if (screen.Exists || !source.Exists || source.Number != 0) return screen;

        return copyXp
            ? source
            : new RosterElementState(0, 0, 0);
    }

    public static bool RostersMatchOwnerSnapshot(TroopRoster snapshot, TroopRoster owner)
    {
        for (int index = 0; index < snapshot.Count; index++)
        {
            var element = snapshot.GetElementCopyAtIndex(index);
            var other = Read(owner, element.Character);
            if (!other.Exists ||
                other.Number != element.Number ||
                other.Wounded != element.WoundedNumber)
            {
                return false;
            }
        }

        for (int index = 0; index < owner.Count; index++)
        {
            var element = owner.GetElementCopyAtIndex(index);
            if (element.Number == 0) continue;

            var other = Read(snapshot, element.Character);
            if (!other.Exists ||
                other.Number != element.Number ||
                other.Wounded != element.WoundedNumber)
            {
                return false;
            }
        }

        return true;
    }

    public static bool HasPopulatedRows(TroopRoster roster)
    {
        for (int index = 0; index < roster.Count; index++)
        {
            if (roster.GetElementNumber(index) != 0) return true;
        }

        return false;
    }
}

internal readonly struct RecruitableState
{
    public readonly bool Exists;
    public readonly int Value;

    public bool IsValid => Value >= 0 && (Exists || Value == 0);

    public RecruitableState(int value)
        : this(value, exists: true)
    {
    }

    public RecruitableState(int value, bool exists)
    {
        Exists = exists;
        Value = value;
    }

    public static RecruitableState Read(
        Dictionary<CharacterObject, int> recruitableData,
        CharacterObject character)
    {
        return recruitableData.TryGetValue(character, out int value)
            ? new RecruitableState(value)
            : default;
    }

    public static void Write(
        Dictionary<CharacterObject, int> recruitableData,
        CharacterObject character,
        RecruitableState state)
    {
        if (!state.IsValid)
            throw new ArgumentOutOfRangeException(nameof(state));

        if (state.Exists)
        {
            recruitableData[character] = state.Value;
        }
        else
        {
            recruitableData.Remove(character);
        }
    }

    public static RecruitableState FromRosterState(
        RosterElementState rosterState,
        bool isHero,
        int conformityNeeded,
        bool hasKey)
    {
        if (!hasKey) return default;

        int value = 0;
        if (rosterState.Exists && !isHero && conformityNeeded > 0)
        {
            value = Math.Min(rosterState.Number, rosterState.Xp / conformityNeeded);
        }

        return new RecruitableState(value);
    }

    public static bool TryRebase(
        RecruitableState current,
        int capacityBefore,
        int capacityAfter,
        bool hasKey,
        out RecruitableState rebased)
    {
        if (!hasKey)
        {
            rebased = default;
            return true;
        }

        long value = (long)current.Value + capacityAfter - capacityBefore;
        if (value < 0 || value > int.MaxValue)
        {
            rebased = default;
            return false;
        }

        rebased = new RecruitableState((int)value);
        return true;
    }
}
