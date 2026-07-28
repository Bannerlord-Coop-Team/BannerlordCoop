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
        return TryRefresh(
            logic,
            authoritativeRoster,
            () => applyAuthoritative(authoritativeRoster, character),
            notifyPendingChangesReset);
    }

    public bool TryRemoveZeroCounts(TroopRoster authoritativeRoster)
    {
        if (authoritativeRoster == null) throw new ArgumentNullException(nameof(authoritativeRoster));
        var logic = (Game.Current?.GameStateManager?.ActiveState as PartyState)?.PartyScreenLogic;
        return TryRefresh(
            logic,
            authoritativeRoster,
            () =>
            {
                authoritativeRoster.RemoveZeroCounts();
                authoritativeRoster.InitializeCachedData();
            },
            NotifyPendingChangesReset);
    }

    private bool TryRefresh(
        PartyScreenLogic logic,
        TroopRoster authoritativeRoster,
        Action applyAuthoritative,
        Action notifyPendingChangesReset)
    {
        var baseline = baselineProvider.GetBaselineRoster(logic, authoritativeRoster);
        if (baseline == null) return false;

        bool hadPendingChanges = logic.IsThereAnyChanges();
        if (hadPendingChanges)
        {
            // The visible roster can be authoritative, so discard local edits before applying the server change.
            logic.CurrentData.ResetUsing(logic._initialData);
        }

        applyAuthoritative();
        // A full snapshot handles rows omitted by vanilla's initial Party-screen clone.
        CopyRoster(authoritativeRoster, baseline);
        var visible = GetVisibleRoster(logic, baseline);
        if (!ReferenceEquals(authoritativeRoster, visible)) CopyRoster(baseline, visible);
        RefreshRecruitablePrisoners(logic);
        RefreshScreen(logic);
        if (hadPendingChanges)
        {
            notifyPendingChangesReset();
        }
        return true;
    }

    private static void CopyRoster(TroopRoster source, TroopRoster destination)
    {
        if (ReferenceEquals(source, destination)) return;

        destination.Clear();
        destination.RemoveZeroCounts();
        foreach (TroopRosterElement element in source.GetTroopRoster())
        {
            if (element.Number == 0) continue;
            destination.Add(element);
        }
        destination.InitializeCachedData();
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
    }

    private static void RefreshScreen(PartyScreenLogic logic)
    {
        logic.OnReset(false);
        if (logic._savedData != null) logic.SavePartyScreenData();
    }

    private static void NotifyPendingChangesReset()
    {
        Logger.Information(PendingChangesResetMessage);
        MBInformationManager.AddQuickInformation(new TextObject(PendingChangesResetMessage));
    }

}
