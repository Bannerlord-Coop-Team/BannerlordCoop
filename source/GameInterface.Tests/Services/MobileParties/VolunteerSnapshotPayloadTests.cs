using Common.Network.Coalescing;
using GameInterface.Services.MobileParties.Messages;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests per-hero merging and ownership of volunteer snapshot data.
/// </summary>
public class VolunteerSnapshotPayloadTests
{
    [Fact]
    public void Merge_KeepsLatestArrayPerHero()
    {
        var first = new VolunteerSnapshotPayload(new Dictionary<string, string[]>
        {
            ["hero_a"] = new[] { "troop_1", "" },
            ["hero_b"] = new[] { "troop_2", "" },
        });
        var incoming = new VolunteerSnapshotPayload(new Dictionary<string, string[]>
        {
            ["hero_a"] = new[] { "troop_3", "troop_4" },
            ["hero_c"] = new[] { "", "troop_5" },
        });

        var merged = first.Merge(incoming);
        var message = Assert.IsType<UpdateVolunteers>(merged.ToMessage());

        Assert.Equal(new[] { "troop_3", "troop_4" }, message.UpdatedVolunteerTypeIds["hero_a"]);
        Assert.Equal(new[] { "troop_2", "" }, message.UpdatedVolunteerTypeIds["hero_b"]);
        Assert.Equal(new[] { "", "troop_5" }, message.UpdatedVolunteerTypeIds["hero_c"]);
    }

    [Fact]
    public void Payload_ClonesInputAndOutputArrays()
    {
        var volunteerTypes = new[] { "troop_1", "" };
        var payload = new VolunteerSnapshotPayload(new Dictionary<string, string[]>
        {
            ["hero_a"] = volunteerTypes,
        });

        volunteerTypes[0] = "mutated_input";
        var firstMessage = Assert.IsType<UpdateVolunteers>(payload.ToMessage());
        firstMessage.UpdatedVolunteerTypeIds["hero_a"][0] = "mutated_output";
        var secondMessage = Assert.IsType<UpdateVolunteers>(payload.ToMessage());

        Assert.Equal("troop_1", secondMessage.UpdatedVolunteerTypeIds["hero_a"][0]);
    }

    [Fact]
    public void Merge_WithDifferentPayloadType_Throws()
    {
        var payload = new VolunteerSnapshotPayload(new Dictionary<string, string[]>());
        var other = new LatestWinsPayload(new UpdateVolunteers(new Dictionary<string, string[]>()));

        Assert.Throws<System.ArgumentException>(() => payload.Merge(other));
    }
}
