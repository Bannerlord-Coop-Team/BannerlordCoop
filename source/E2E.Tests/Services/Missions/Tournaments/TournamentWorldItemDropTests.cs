using Missions.Agents.Messages;
using ProtoBuf;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentWorldItemDropTests
{
    [Fact]
    public void ReplicatedWeaponDrop_CarriesRuntimeWorldItemId()
    {
        Guid worldItemId = Guid.NewGuid();
        var message = new NetworkWeaponDropped(
            Guid.NewGuid(),
            EquipmentIndex.Weapon0,
            worldItemId);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;

        NetworkWeaponDropped received = Serializer.Deserialize<NetworkWeaponDropped>(stream);

        Assert.Equal(worldItemId, received.WorldItemId);
    }
}
