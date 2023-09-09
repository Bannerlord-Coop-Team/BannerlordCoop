// Ignore Spelling: Guids

using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using ProtoBuf;

namespace Coop.Core.Client.Messages
{
    /// <summary>
    /// Received Game save data from the network event
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkGameSaveDataReceived : IEvent
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
