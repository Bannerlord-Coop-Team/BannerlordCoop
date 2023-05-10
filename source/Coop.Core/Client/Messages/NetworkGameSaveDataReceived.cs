// Ignore Spelling: Guids

using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using System;
using GameInterface.Services.Heroes.Data;

namespace Coop.Core.Client.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkGameSaveDataReceived : INetworkEvent
    {
        [ProtoMember(1)]
        public byte[] GameSaveData { get; }
        [ProtoMember(2)]
        public string CampaignID { get; }
        [ProtoMember(3)]
        public GameObjectGuids GameObjectGuids { get; }

        public NetworkGameSaveDataReceived(
            byte[] gameSaveData,
            string campaignID,
            GameObjectGuids gameObjectGuids)
        {
            GameSaveData = gameSaveData;
            CampaignID = campaignID;
            GameObjectGuids = gameObjectGuids;
        }
    }
}
