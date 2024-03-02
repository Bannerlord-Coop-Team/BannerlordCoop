using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Utils.AutoSync;

public interface ISyncResults
{
    Type DataType { get; }
    Type EventType { get; }
    Type NetworkMessageType { get; }
    Type HandlerType { get; }
    IEnumerable<Type> SerializableTypes { get; }
}

public record SyncResults : ISyncResults
{
    public Type DataType { get; set; }
    public Type EventType { get; set; }
    public Type NetworkMessageType { get; set; }
    public Type HandlerType { get; set; }

    public IEnumerable<Type> SerializableTypes { get; set; }
}