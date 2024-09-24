using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeEngineConstructionProgressss.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkSiegeEngineConstructionProgressChangeProperty : ICommand
    {
        [ProtoMember(1)]
        public string propertyName;
        [ProtoMember(2)]
        public string siegeEngineConstructionProgressId;
        [ProtoMember(3)]
        public string objectId;
        [ProtoMember(4)]
        public byte[] serializedValue;

        public NetworkSiegeEngineConstructionProgressChangeProperty(string propertyName, string besiegerCampId, string objectId)
        {
            this.propertyName = propertyName;
            this.siegeEngineConstructionProgressId = besiegerCampId;
            this.objectId = objectId;
        }

        public NetworkSiegeEngineConstructionProgressChangeProperty(string propertyName, string besiegerCampId, byte[] serializedValue)
        {
            this.propertyName = propertyName;
            this.siegeEngineConstructionProgressId = besiegerCampId;
            this.serializedValue = serializedValue;
        }
    }
}