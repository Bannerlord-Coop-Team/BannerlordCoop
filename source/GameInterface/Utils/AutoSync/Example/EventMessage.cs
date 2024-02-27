using Common.Messaging;
using GameInterface.Utils.AutoSync.Template;

namespace GameInterface.Utils.AutoSync.Example;
internal class EventMessage : IEvent, IAutoSyncMessage<int>
{
    public IAutoSyncData<int> Data { get; }

    public EventMessage(IAutoSyncData<int> data)
    {
        Data = data;
    }
}
