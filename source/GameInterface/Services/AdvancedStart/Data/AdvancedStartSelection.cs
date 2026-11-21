using ProtoBuf;

namespace GameInterface.Services.AdvancedStart.Data;

/// <summary>One advanced starting option a joining player picked, keyed by the vanilla option id.</summary>
[ProtoContract(SkipConstructor = true)]
public class AdvancedStartSelection
{
    [ProtoMember(1)]
    public readonly string Id;
    [ProtoMember(2)]
    public readonly string Value;

    public AdvancedStartSelection(string id, string value)
    {
        Id = id;
        Value = value;
    }
}
