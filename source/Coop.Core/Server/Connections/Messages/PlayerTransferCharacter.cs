using Common.Messaging;
using GameInterface.Serialization.External;
using LiteNetLib;
using ProtoBuf;
using System;

namespace Coop.Core.Server.Connections.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct PlayerTransferCharacter : INetworkEvent
    {
        public PlayerTransferCharacter(byte[] playerHero)
        {
            PlayerHero = playerHero;
        }

        [ProtoMember(1)]
        public byte[] PlayerHero { get; }
    }
}
