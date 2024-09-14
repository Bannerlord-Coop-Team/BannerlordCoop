using Common.Messaging;
using GameInterface.Services.Workshops.Data;

namespace GameInterface.Services.Workshops.Messages;
internal record WorkshopCreated(WorkshopCreatedData Data) : IEvent
{
    public WorkshopCreatedData Data { get; } = Data;
}