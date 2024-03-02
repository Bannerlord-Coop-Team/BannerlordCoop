using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Utils.AutoSync;

/// <summary>
/// Results from syncing a property
/// </summary>
public interface ISyncResults
{
    /// <summary>
    /// Generated Data class type
    /// </summary>
    Type DataType { get; }

    /// <summary>
    /// Generated Event class type
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// Generated NetworkMessage class type
    /// </summary>
    Type NetworkMessageType { get; }

    /// <summary>
    /// Generated Handler class type
    /// </summary>
    Type HandlerType { get; }

    /// <summary>
    /// Enumerable of all serializable types (to be added to <see cref="ISerializableTypeMapper"/>)"/>
    /// </summary>
    IEnumerable<Type> SerializableTypes { get; }
}

/// <inheritdoc cref="ISyncResults"/>
public record SyncResults : ISyncResults
{
    public Type DataType { get; set; }
    public Type EventType { get; set; }
    public Type NetworkMessageType { get; set; }
    public Type HandlerType { get; set; }

    public IEnumerable<Type> SerializableTypes { get; set; }
}