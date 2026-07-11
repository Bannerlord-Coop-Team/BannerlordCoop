using Common.Network;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventInitializationBarrierTests
{
    [Fact]
    public void Dispose_ReleasesPendingPartyLocks()
    {
        var barrier = new MapEventInitializationBarrier(
            new Mock<INetwork>().Object,
            new Mock<IObjectManager>().Object);
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        var party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));

        barrier.RegisterClient(mapEvent);
        barrier.LockClientParty(mapEvent, party);
        Assert.True(barrier.IsPartyPending(party));

        barrier.Dispose();
        barrier.LockClientParty(mapEvent, party);

        Assert.False(barrier.IsPartyPending(party));
    }
}
