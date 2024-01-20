using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Heroes.Messages
{
    /// <summary>
    /// Take prisoner is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkTakePrisonerApproved : ICommand
    {
        [ProtoMember(1)]
        public string PartyId { get; }
        [ProtoMember(2)]
        public string CharacterId { get; }
        [ProtoMember(3)]
        public bool IsEventCalled { get; }

        public NetworkTakePrisonerApproved(string partyId, string characterId, bool isEventCalled)
        {
            PartyId = partyId;
            CharacterId = characterId;
            IsEventCalled = isEventCalled;
        }
    }
}
