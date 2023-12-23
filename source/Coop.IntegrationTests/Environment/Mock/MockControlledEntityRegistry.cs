using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Data;

namespace Coop.IntegrationTests.Environment.Mock;

internal class MockControlledEntityRegistry : IControlledEntityRegistry
{
    public bool IsControlledBy(string controllerId, string entityId)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyDictionary<string, Common.IReadOnlySet<ControlledEntity>> PackageControlledEntities()
    {
        throw new NotImplementedException();
    }

    public bool RegisterAsControlled(string controllerId, string entityId)
    {
        throw new NotImplementedException();
    }

    public bool RegisterAsControlled(string controllerId, string entityId, out ControlledEntity newEntity)
    {
        throw new NotImplementedException();
    }

    public void RegisterExistingEntities(IEnumerable<ControlledEntity> entityIds)
    {
        throw new NotImplementedException();
    }

    public bool RemoveAsControlled(ControlledEntity entityId)
    {
        throw new NotImplementedException();
    }

    public bool TryGetControlledEntities(string controllerId, out IEnumerable<ControlledEntity> controlledEntities)
    {
        throw new NotImplementedException();
    }

    public bool TryGetControlledEntity(string entityId, out ControlledEntity entity)
    {
        throw new NotImplementedException();
    }
}
