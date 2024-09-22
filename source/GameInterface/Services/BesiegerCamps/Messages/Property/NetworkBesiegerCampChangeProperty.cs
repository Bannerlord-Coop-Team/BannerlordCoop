using Common.Messaging;
using GameInterface.Services.BesiegerCamps.Patches;
using ProtoBuf;

namespace GameInterface.Services.BesiegerCampss.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkBesiegerCampChangeProperty : ICommand
    {
        [ProtoMember(1)]
        public string propertyName;
        [ProtoMember(2)]
        public string besiegerCampId;
        [ProtoMember(3)]
        public string objectId;
        [ProtoMember(4)]
        public byte[] serializedValue;

        public NetworkBesiegerCampChangeProperty(string propertyName, string besiegerCampId, string objectId)
        {
            this.propertyName = propertyName;
            this.besiegerCampId = besiegerCampId;
            this.objectId = objectId;
        }

        public NetworkBesiegerCampChangeProperty(string propertyName, string besiegerCampId, byte[] serializedValue)
        {
            this.propertyName = propertyName;
            this.besiegerCampId = besiegerCampId;
            this.serializedValue = serializedValue;
        }
    }
}
