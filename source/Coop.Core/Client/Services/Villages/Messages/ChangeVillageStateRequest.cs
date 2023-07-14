﻿using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Villages.Messages
{
    /// <summary>
    /// Request to change village state from client to server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record ChangeVillageStateRequest : ICommand
    {
        [ProtoMember(1)]
        public string VillageId { get; }
        [ProtoMember(2)]
        public int NewState { get; }
        [ProtoMember(3)]
        public string PartyId { get; }

        public ChangeVillageStateRequest(string villageId, int newState, string partyId)
        {
            VillageId = villageId;
            NewState = newState;
            PartyId = partyId;
        }
    }
}