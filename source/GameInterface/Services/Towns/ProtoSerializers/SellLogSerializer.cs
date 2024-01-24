using ProtoBuf;

namespace GameInterface.Services.Towns.ProtoSerializers
{
    [ProtoContract(SkipConstructor = true)]
    public class SellLogSerializer
    {
        [ProtoMember(1)]
        public int Number { get; }

        [ProtoMember(2)]
        public string CategoryID { get; }
        public SellLogSerializer(int number, string categoryID)
        {
            Number = number;
            CategoryID = categoryID;
        }
    }
}
