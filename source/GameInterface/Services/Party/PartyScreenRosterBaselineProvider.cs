using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Party;

internal interface IPartyScreenRosterBaselineProvider
{
    TroopRoster GetBaselineRoster(TroopRoster roster);
}

internal class PartyScreenRosterBaselineProvider : IPartyScreenRosterBaselineProvider
{
    public TroopRoster GetBaselineRoster(TroopRoster roster)
    {
        var logic = Game.Current?.GameStateManager?.LastOrDefault<PartyState>()?.PartyScreenLogic;
        return GetBaselineRoster(logic, roster);
    }

    internal TroopRoster GetBaselineRoster(PartyScreenLogic logic, TroopRoster roster)
    {
        if (logic == null || roster == null)
            return null;

        if (ReferenceEquals(roster, logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right]))
            return logic._initialData.RightMemberRoster;
        if (ReferenceEquals(roster, logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left]))
            return logic._initialData.LeftMemberRoster;
        if (ReferenceEquals(roster, logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right]))
            return logic._initialData.RightPrisonerRoster;
        if (ReferenceEquals(roster, logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left]))
            return logic._initialData.LeftPrisonerRoster;

        return null;
    }
}
