using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Messages
{
    [ProtoContract]
    public readonly struct NetworkGameSaveDataRecieved : INetworkEvent
    {
        [ProtoMember(1)]
        public byte[] GameSaveData { get; }
        public NetworkGameSaveDataRecieved(byte[] gameSaveData)
        {
            GameSaveData = gameSaveData;
        }
    }
}
