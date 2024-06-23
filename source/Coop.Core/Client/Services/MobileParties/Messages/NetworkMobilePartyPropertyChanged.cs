using Common.Messaging;
using ProtoBuf;
#nullable enable

namespace Coop.Core.Client.Services.MobileParties.Messages
{
    /// <summary>
    /// Client publish for party properties
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkMobilePartyPropertyChanged : ICommand
    {
        [ProtoMember(1)]
        public int PropertyType { get; }
        [ProtoMember(2)]
        public string Value1 { get; }
        [ProtoMember(3)]
        public string Value2 { get; }
        [ProtoMember(4)]
        public string? Value3 { get; }

        public NetworkMobilePartyPropertyChanged(int propertyType, string value1, string value2, string? value3)
        {
            PropertyType = propertyType;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
        }
    }
}
