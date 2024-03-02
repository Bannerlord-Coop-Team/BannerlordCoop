using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Utils.AutoSync.Template;

/// <summary>
/// Interface for auto sync data
/// </summary>
/// <typeparam name="DataType">Type of data to sync, this will be either a field or property type. This type must also be serializable by protobuf</typeparam>
public interface IAutoSyncData<DataType>
{
    string StringId { get; }
    DataType Value { get; }
}

/// <summary>
/// Interface for auto sync messages
/// </summary>
/// <typeparam name="DataType">Type of data to sync, this will be either a field or property type. This type must also be serializable by protobuf</typeparam>
public interface IAutoSyncMessage<DataType> : IMessage
{
    IAutoSyncData<DataType> Data { get; }
}