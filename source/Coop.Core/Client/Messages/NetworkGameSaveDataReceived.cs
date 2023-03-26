using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using System;

namespace Coop.Core.Client.Messages
{
    [ProtoContract]
    public readonly struct NetworkGameSaveDataReceived : INetworkEvent
    {
        [ProtoMember(1)]
        public byte[] GameSaveData { get; }
        [ProtoMember(2)]
        public string CampaignID { get; }
        [ProtoMember(3)]
        public ISet<Guid> ControlledHeros { get; }
        [ProtoMember(4)]
        public IReadOnlyDictionary<string, Guid> PartyIds { get; }
        [ProtoMember(5)]
        public IReadOnlyDictionary<string, Guid> HeroIds { get; }

        public NetworkGameSaveDataReceived(
            byte[] gameSaveData,
            string campaignID,
            ISet<Guid> controlledHeros,
            IReadOnlyDictionary<string, Guid> partyIds,
            IReadOnlyDictionary<string, Guid> heroIds)
        {
            GameSaveData = gameSaveData;
            CampaignID = campaignID;
            ControlledHeros = controlledHeros;
            PartyIds = partyIds;
            HeroIds = heroIds;
        }
    }
}
