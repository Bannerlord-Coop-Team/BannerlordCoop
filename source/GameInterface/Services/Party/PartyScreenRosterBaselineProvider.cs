using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party;

internal interface IPartyScreenRosterBaselineProvider
{
    TroopRoster GetBaselineRoster(PartyScreenLogic logic, TroopRoster roster);

    ItemRoster GetBaselineRoster(PartyScreenLogic logic, ItemRoster roster);
}

internal class PartyScreenRosterBaselineProvider : IPartyScreenRosterBaselineProvider
{
    public TroopRoster GetBaselineRoster(PartyScreenLogic logic, TroopRoster roster)
    {
        if (logic == null || roster == null)
            return null;

        if (ReferenceEquals(roster, logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right]) ||
            ReferenceEquals(roster, logic.RightOwnerParty?.MemberRoster))
            return logic._initialData.RightMemberRoster;
        if (ReferenceEquals(roster, logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left]) ||
            ReferenceEquals(roster, logic.LeftOwnerParty?.MemberRoster))
            return logic._initialData.LeftMemberRoster;
        if (ReferenceEquals(roster, logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right]) ||
            ReferenceEquals(roster, logic.RightOwnerParty?.PrisonRoster))
            return logic._initialData.RightPrisonerRoster;
        if (ReferenceEquals(roster, logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left]) ||
            ReferenceEquals(roster, logic.LeftOwnerParty?.PrisonRoster))
            return logic._initialData.LeftPrisonerRoster;

        return null;
    }

    public ItemRoster GetBaselineRoster(PartyScreenLogic logic, ItemRoster roster)
    {
        if (logic == null || roster == null) return null;
        return ReferenceEquals(roster, logic.CurrentData.RightItemRoster)
            ? logic._initialData.RightItemRoster
            : null;
    }
}
