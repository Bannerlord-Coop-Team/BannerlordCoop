using Common.Messaging;
using GameInterface.Services.Equipments.Data;
using ProtoBuf;

namespace GameInterface.Services.Alleys.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkCreateAlley : ICommand
    {
        [ProtoMember(1)]
        public string AlleyId { get; }

        [ProtoMember(2)]
        public string SettlementId { get; }

        [ProtoMember(3)]
        public string Tag { get; }

        [ProtoMember(4)]
        public string Name { get; }

        public NetworkCreateAlley(string AlleyId, string SettlementId, string Tag, string Name)
        {
            this.AlleyId = AlleyId;
            this.SettlementId = SettlementId;
            this.Tag = Tag;
            this.Name = Name;
        }
    }
}