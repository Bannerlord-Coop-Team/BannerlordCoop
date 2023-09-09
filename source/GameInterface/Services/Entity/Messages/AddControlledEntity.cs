using Common.Messaging;

namespace GameInterface.Services.Entity.Messages;

public record AddControlledEntity : ICommand
{
    public string ControllerId { get; }
    public string EntityId { get; }

    public AddControlledEntity(string controllerId, string entityId)
    {
        ControllerId = controllerId;
        EntityId = entityId;
    }
}
