using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Heroes.Messages
{
    /// <summary>
    /// Request from client to server to take prisoner
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkTakePrisonerRequest : ICommand
    {
        [ProtoMember(1)]
        public string PartyId { get; }
        [ProtoMember(2)]
        public string CharacterId { get; }
        [ProtoMember(3)]
        public bool IsEventCalled { get; }

        public NetworkTakePrisonerRequest(string partyId, string characterId, bool isEventCalled)
        {
            PartyId = partyId;
            CharacterId = characterId;
            IsEventCalled = isEventCalled;
        }
    }
}
