using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Utils.AutoSync.Example;

[ProtoContract(SkipConstructor = true)]
public record MessageData
{
    public MessageData(string stringId, int value)
    {
        StringId = stringId;
        Value = value;
    }

    [ProtoMember(1)]
    public string StringId { get; }
    [ProtoMember(2)]
    public int Value { get; }
}
