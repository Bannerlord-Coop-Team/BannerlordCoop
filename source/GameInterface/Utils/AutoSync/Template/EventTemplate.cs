using Common.Messaging;

namespace GameInterface.Utils.AutoSync.Template;
internal class EventTemplate<DataType> : IEvent, IAutoSyncMessage<DataType>
{
    public IAutoSyncData<DataType> Data { get; }

    public EventTemplate(IAutoSyncData<DataType> data)
    {
        Data = data;
    }
}
