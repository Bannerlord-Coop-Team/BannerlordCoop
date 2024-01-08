using Common.Serialization;
using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Data;
using GameInterface.Services.GameDebug.Messages;
using SandBox;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using Xunit;

namespace GameInterface.Tests.Services.Entity;

public class ControlledEntityRegistryTests
{
    [Fact]
    public void Serialization()
    {
        var ownerId = "Server";

        ControlledEntityRegistry registry = new ControlledEntityRegistry();

        var existingEntities = new ControlledEntity[]
        {
            new ControlledEntity(ownerId, "Entity1"),
            new ControlledEntity(ownerId, "Entity2"),
            new ControlledEntity(ownerId, "Entity3"),
        };

        registry.RegisterExistingEntities(existingEntities);

        var bytes = ProtoBufSerializer.Serialize(registry);

        var deserializedRegistry = ProtoBufSerializer.Deserialize<ControlledEntityRegistry>(bytes);

        Assert.Equal(registry, deserializedRegistry);
    }
}
