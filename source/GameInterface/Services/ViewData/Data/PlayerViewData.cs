using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.ViewData.Data;

/// <summary>
/// One player's copy of the saved fields in ViewDataTrackerCampaignBehavior
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class PlayerViewData
{
    [ProtoMember(1)]
    public bool IsMapBarExtended { get; set; }

    [ProtoMember(2)]
    public int PartySortType { get; set; }

    [ProtoMember(3)]
    public bool IsPartySortAscending { get; set; }

    [ProtoMember(4)]
    public List<string> PartyTroopLocks { get; set; }

    [ProtoMember(5)]
    public List<string> PartyPrisonerLocks { get; set; }

    [ProtoMember(6)]
    public int QuestSortTypeSelection { get; set; }

    [ProtoMember(7)]
    public bool IsCharacterNotificationActive { get; set; }

    [ProtoMember(8)]
    public int NumOfPerks { get; set; }

    [ProtoMember(9)]
    public List<string> BookmarkedHeroIds { get; set; }

    [ProtoMember(10)]
    public List<string> BookmarkedShipHullIds { get; set; }

    [ProtoMember(11)]
    public List<string> BookmarkedClanIds { get; set; }

    [ProtoMember(12)]
    public List<string> BookmarkedConceptIds { get; set; }

    [ProtoMember(13)]
    public List<string> BookmarkedKingdomIds { get; set; }

    [ProtoMember(14)]
    public List<string> BookmarkedSettlementIds { get; set; }

    [ProtoMember(15)]
    public List<string> BookmarkedUnitIds { get; set; }

    [ProtoMember(16)]
    public string QuestSelectionId { get; set; }

    [ProtoMember(17)]
    public List<string> UnExaminedArmyIds { get; set; }

    // Dictionary<CharacterObjectId, RecruitableCount>
    [ProtoMember(18)]
    public Dictionary<string, int> ExaminedPrisonerCharacters { get; set; }

    [ProtoMember(19)]
    public List<string> UnexaminedFigureheadIds { get; set; }

    // Map pins kept in VisualTrackerManager, not ViewDataTrackerCampaignBehavior
    [ProtoMember(20)]
    public List<string> TrackedSettlementIds { get; set; }

    [ProtoMember(21)]
    public List<string> TrackedPartyIds { get; set; }
}
