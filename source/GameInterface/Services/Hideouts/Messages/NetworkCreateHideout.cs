using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Hideouts.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateHideout : ICommand
{
    [ProtoMember(1)]
    public string HideoutId { get; }

    public NetworkCreateHideout(string hideoutId)
    {
        HideoutId = hideoutId;
    }
}
