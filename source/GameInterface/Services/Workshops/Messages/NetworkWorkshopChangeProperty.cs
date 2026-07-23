using Common.Messaging;
using GameInterface.Services.Workshops.Patches;
using ProtoBuf;

namespace GameInterface.Services.Workshops.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkWorkshopChangeProperty : ICommand
    {
        [ProtoMember(1)]
        public PropertyType _propertyType;
        [ProtoMember(2)]
        public string workshopId;
        [ProtoMember(3)]
        public string mainData;
        [ProtoMember(4)]
        public string extraData;

        public NetworkWorkshopChangeProperty(PropertyType propertyType, string workshopId, string mainData, string extraData)
        {
            _propertyType = propertyType;
            this.workshopId = workshopId;
            this.mainData = mainData;
            this.extraData = extraData;
        }
    }
}
