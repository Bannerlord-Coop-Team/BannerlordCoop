using Common.Messaging;
using GameInterface.Serialization.External;
using LiteNetLib;
using ProtoBuf;
using System;

namespace Coop.Core.Server.Connections.Messages.Incoming
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkTransferedHero : INetworkEvent
    {
        public NetworkTransferedHero(byte[] playerHero)
        {
            PlayerHero = playerHero;
        }

        [ProtoMember(1)]
        public byte[] PlayerHero { get; }
    }
}
