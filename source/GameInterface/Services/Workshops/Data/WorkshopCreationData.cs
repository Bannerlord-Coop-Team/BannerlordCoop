using ProtoBuf;

namespace GameInterface.Services.Workshops.Data
{
    [ProtoContract(SkipConstructor = true)]
    internal record WorkshopCreatedData
    {
        [ProtoMember(1)]
        public string WorkshopId { get; }
        [ProtoMember(2)]
        public string SettlementId { get; }
        [ProtoMember(3)]
        public string Tag { get; }
        [ProtoMember(4)]
        public uint WorkshopHandle { get; }
        [ProtoMember(5)]
        public uint SettlementHandle { get; }

        public WorkshopCreatedData(
            string workshopId,
            string settlementId,
            string tag,
            uint workshopHandle,
            uint settlementHandle)
        {
            WorkshopId = workshopId;
            SettlementId = settlementId;
            Tag = tag;
            WorkshopHandle = workshopHandle;
            SettlementHandle = settlementHandle;
        }
    }
}
