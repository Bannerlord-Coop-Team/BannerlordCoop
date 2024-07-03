using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Network message to command the destruction of a clan
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroyClan : ICommand
{
    [ProtoMember(1)]
    public ClanDestroyedData Data { get; }

    public NetworkDestroyClan(ClanDestroyedData data)
    {
        Data = data;
    }
}
