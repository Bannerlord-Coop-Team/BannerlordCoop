using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.WorkshopTypes.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkChangeProductions : ICommand
    {
        [ProtoMember(1)]
        public string workshopTypeId;
        [ProtoMember(2)]
        public bool isAdd;
        [ProtoMember(3)]
        public float conversionSpeed;

        public NetworkChangeProductions(string workshopTypeId, bool isAdd, float conversionSpeed)
        {
            this.workshopTypeId = workshopTypeId;
            this.isAdd = isAdd;
            this.conversionSpeed = conversionSpeed;
        }
    }
}
