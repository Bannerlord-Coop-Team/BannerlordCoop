using System;
using ProtoBuf;

namespace GameInterface.Serialization.Models
{
    [ProtoContract]
    public class SettlementSurrogate
    {
        [ProtoMember(1)] 
        public readonly Guid ClaimedBy;
    }
}