using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.EquipmentRoster.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateEquipmentRoster : ICommand
    {
        [ProtoMember(1)]
        public string EquipmentRosterId;
        public NetworkCreateEquipmentRoster(string equipmentRosterId)
        {
            EquipmentRosterId = equipmentRosterId;
        }
    }
}
