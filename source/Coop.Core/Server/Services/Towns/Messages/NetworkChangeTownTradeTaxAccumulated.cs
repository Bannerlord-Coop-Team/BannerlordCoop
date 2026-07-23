using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes TradeTaxAccumulated.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkChangeTownTradeTaxAccumulated : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2)]
        public int TradeTaxAccumulated { get; }

        public NetworkChangeTownTradeTaxAccumulated(string townId, int tradeTaxAccumulated)
        {
            TownId = townId;
            TradeTaxAccumulated = tradeTaxAccumulated;
        }
    }
}
