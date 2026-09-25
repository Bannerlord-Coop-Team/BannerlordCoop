using Common.Network.Coalescing;
using GameInterface.Services.MobileParties.Messages;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests per-hero merging and ownership of volunteer snapshot data.
/// </summary>
public class VolunteerSnapshotPayloadTests
{
    [Theory]
    [InlineData("GameInterface.Services.Heroes.Messages.Collections.NetworkUpdateArray")]
    [InlineData("GameInterface.Services.MobileParties.Messages.RemoveVolunteer")]
    public void LegacySingleOperationMessages_AreNotInAssembly(string typeName)
    {
        Assembly gameInterfaceAssembly = typeof(UpdateVolunteers).Assembly;

        Assert.Null(gameInterfaceAssembly.GetType(typeName));
    }

    [Fact]
    public void Merge_KeepsLatestArrayPerHero()
    {
        var first = new VolunteerSnapshotPayload(new Dictionary<uint, uint[]>
        {
            [1] = new uint[] { 4, 0 },
            [2] = new uint[] { 5, 0 },
        });
        var incoming = new VolunteerSnapshotPayload(new Dictionary<uint, uint[]>
        {
            [1] = new uint[] { 6, 7 },
            [3] = new uint[] { 0, 8 },
        });

        var merged = first.Merge(incoming);
        var message = Assert.IsType<UpdateVolunteers>(merged.ToMessage());

        Assert.Equal(new uint[] { 6, 7 }, message.UpdatedVolunteerTypeIds[1]);
        Assert.Equal(new uint[] { 5, 0 }, message.UpdatedVolunteerTypeIds[2]);
        Assert.Equal(new uint[] { 0, 8 }, message.UpdatedVolunteerTypeIds[3]);
    }

    [Fact]
    public void Payload_ClonesInputAndOutputArrays()
    {
        var volunteerTypes = new uint[] { 4, 0 };
        var payload = new VolunteerSnapshotPayload(new Dictionary<uint, uint[]>
        {
            [1] = volunteerTypes,
        });

        volunteerTypes[0] = 9;
        var firstMessage = Assert.IsType<UpdateVolunteers>(payload.ToMessage());
        firstMessage.UpdatedVolunteerTypeIds[1][0] = 10;
        var secondMessage = Assert.IsType<UpdateVolunteers>(payload.ToMessage());

        Assert.Equal(4u, secondMessage.UpdatedVolunteerTypeIds[1][0]);
    }

    [Fact]
    public void Merge_WithDifferentPayloadType_Throws()
    {
        var payload = new VolunteerSnapshotPayload(new Dictionary<uint, uint[]>());
        var other = new LatestWinsPayload(new UpdateVolunteers(new Dictionary<uint, uint[]>()));

        Assert.Throws<System.ArgumentException>(() => payload.Merge(other));
    }
}
