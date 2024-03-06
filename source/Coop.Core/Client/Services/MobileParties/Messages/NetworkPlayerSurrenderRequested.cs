using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MobileParties.Messages
{
    /// <summary>
    /// Request to surrender from client
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkPlayerSurrenderRequested : ICommand
    {
        [ProtoMember(1)]
        public string PlayerPartyId;
        [ProtoMember(2)]
        public string CaptorPartyId;
        [ProtoMember(3)]
        public string CharacterId;

        public NetworkPlayerSurrenderRequested(string playerPartyId, string captorPartyId, string characterId)
        {
            PlayerPartyId = playerPartyId;
            CaptorPartyId = captorPartyId;
            CharacterId = characterId;
        }
    }
}