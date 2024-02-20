using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Utils.AutoSync.Example;

[ProtoContract(SkipConstructor = true)]
public class NetworkMessage : ICommand
{
    public NetworkMessage(MessageData data)
    {
        Data = data;
    }

    [ProtoMember(1)]
    public MessageData Data { get; }
}
