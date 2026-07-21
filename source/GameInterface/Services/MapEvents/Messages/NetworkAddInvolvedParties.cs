using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAddInvolvedParties : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string[] MapEventPartyIds;
    /// <summary>
    /// Server-side map positions of the involved parties, index-aligned with
    /// <see cref="MapEventPartyIds"/>. Clients snap each party to its position so the
    /// battle and its parties line up. Settlement parties have no map position and
    /// occupy a default, unused slot.
    /// </summary>
    [ProtoMember(3)]
    public readonly CampaignVec2[] Positions;
    /// <summary>Server-granted initial slots, index-aligned with <see cref="MapEventPartyIds"/>.</summary>
    [ProtoMember(4)]
    public readonly int[] InitialSpawnCounts;
    /// <summary>Whether each party was added to the frozen spawn plan by this broadcast.</summary>
    [ProtoMember(5)]
    public readonly bool[] PostPlanAdditions;

    public NetworkAddInvolvedParties(
        string mapEventId,
        string[] mapEventPartyIds,
        CampaignVec2[] positions,
        int[] initialSpawnCounts,
        bool[] postPlanAdditions)
    {
        MapEventId = mapEventId;
        MapEventPartyIds = mapEventPartyIds;
        Positions = positions;
        InitialSpawnCounts = initialSpawnCounts;
        PostPlanAdditions = postPlanAdditions;
    }
}
