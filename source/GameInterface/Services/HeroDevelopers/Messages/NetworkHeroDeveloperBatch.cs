using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.HeroDevelopers.Messages;

/// <summary>
/// Identifies one ordered operation in a hero-developer network batch.
/// </summary>
[ProtoContract]
public enum NetworkHeroDeveloperOperationType
{
    [ProtoEnum]
    RawXpGain,

    [ProtoEnum]
    SkillXpSet,

    [ProtoEnum]
    SkillLevelChange,
}

/// <summary>
/// Serializable form of one hero-developer operation.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkHeroDeveloperOperation
{
    [ProtoMember(1)]
    public NetworkHeroDeveloperOperationType Type;

    [ProtoMember(2)]
    public string SkillObjectId;

    [ProtoMember(3)]
    public float Value;

    [ProtoMember(4)]
    public int ChangeAmount;

    [ProtoMember(5)]
    public bool ShouldNotify;

    public NetworkHeroDeveloperOperation(
        NetworkHeroDeveloperOperationType type,
        string skillObjectId,
        float value,
        int changeAmount,
        bool shouldNotify)
    {
        Type = type;
        SkillObjectId = skillObjectId;
        Value = value;
        ChangeAmount = changeAmount;
        ShouldNotify = shouldNotify;
    }
}

/// <summary>
/// Sends one ordered hero-developer batch from a client to the server.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkHeroDeveloperBatchServer : ICommand
{
    public const int MaxOperationsPerMessage = 16;

    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public List<NetworkHeroDeveloperOperation> Operations;

    public NetworkHeroDeveloperBatchServer(
        string heroId,
        List<NetworkHeroDeveloperOperation> operations)
    {
        HeroId = heroId;
        Operations = operations;
    }
}

/// <summary>
/// Broadcasts one authoritative ordered hero-developer batch to clients.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkHeroDeveloperBatchClients : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public List<NetworkHeroDeveloperOperation> Operations;

    public NetworkHeroDeveloperBatchClients(
        string heroId,
        List<NetworkHeroDeveloperOperation> operations)
    {
        HeroId = heroId;
        Operations = operations == null
            ? new List<NetworkHeroDeveloperOperation>()
            : new List<NetworkHeroDeveloperOperation>(operations);
    }

    public NetworkHeroDeveloperBatchClients(NetworkHeroDeveloperBatchServer cloneObject)
        : this(cloneObject.HeroId, cloneObject.Operations)
    {
    }
}
