using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Utils.AutoSync.Template;


public interface IAutoSyncData<DataType>
{
    string StringId { get; }
    DataType Value { get; }
}

public interface IAutoSyncMessage<DataType> : IMessage
{
    IAutoSyncData<DataType> Data { get; }
}