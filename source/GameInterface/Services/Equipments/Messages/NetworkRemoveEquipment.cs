using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Equipments.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkRemoveEquipment : ICommand
    {
        [ProtoMember(1)]
        public string BattleEquipmentId { get; }

        [ProtoMember(2)]
        public string CivilEquipmentId { get; }

        public NetworkRemoveEquipment(string battleEquipmentId, string civilEquipmentId)
        {
            BattleEquipmentId = battleEquipmentId;
            CivilEquipmentId = civilEquipmentId;
        }
    }
}
