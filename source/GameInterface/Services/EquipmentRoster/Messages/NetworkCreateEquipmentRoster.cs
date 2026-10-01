using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.EquipmentRoster.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateEquipmentRoster : ICommand
    {
        [ProtoMember(1)]
        public string EquipmentRosterId;
        [ProtoMember(2)]
        public uint Handle;
        public NetworkCreateEquipmentRoster(string equipmentRosterId, uint handle)
        {
            EquipmentRosterId = equipmentRosterId;
            Handle = handle;
        }
    }
}
