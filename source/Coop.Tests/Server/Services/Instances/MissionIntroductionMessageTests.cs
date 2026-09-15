using Common.Serialization;
using Missions.Messages;
using System;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

public class MissionIntroductionMessageTests
{
    [Fact]
    public void RequestRoundTripsTheMissionEntryRequestId()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var request = new NetworkRequestMissionIntroduction("town_ES1|tavern", Guid.NewGuid());

        var received = serializer.Deserialize<NetworkRequestMissionIntroduction>(serializer.Serialize(request));

        Assert.Equal(request.InstanceId, received.InstanceId);
        Assert.Equal(request.RequestId, received.RequestId);
    }

    [Fact]
    public void AuthorizationRoundTripsTheRequestAndSessionToken()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        string token = Guid.NewGuid().ToString("N");
        var authorization = new NetworkMissionIntroductionAuthorized("town_ES1|tavern", Guid.NewGuid(), token);

        var received = serializer.Deserialize<NetworkMissionIntroductionAuthorized>(serializer.Serialize(authorization));

        Assert.Equal(authorization.InstanceId, received.InstanceId);
        Assert.Equal(authorization.RequestId, received.RequestId);
        Assert.Equal(authorization.Token, received.Token);
        Assert.True(Guid.TryParseExact(received.Token, "N", out _));
    }
}
