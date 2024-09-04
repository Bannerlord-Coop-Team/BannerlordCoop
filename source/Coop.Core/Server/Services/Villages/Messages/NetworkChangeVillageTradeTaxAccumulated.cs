using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Villages.Messages;

/// <summary>
/// message sent for TradeTaxAccumulated changes 
/// </summary>
[ProtoContract(SkipConstructor = true)]
[DontLogMessage]
internal class NetworkChangeVillageTradeTaxAccumulated : IEvent
{
    [ProtoMember(1)]
    public string VillageId { get; }

    [ProtoMember(2)]
    public int TradeTaxAccumulated { get; }

    public NetworkChangeVillageTradeTaxAccumulated(string villageId, int tradeTaxAccumulated)
    {
        VillageId = villageId;
        TradeTaxAccumulated = tradeTaxAccumulated;
    }
}
