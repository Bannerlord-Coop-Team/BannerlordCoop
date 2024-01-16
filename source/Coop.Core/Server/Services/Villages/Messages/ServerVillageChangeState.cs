using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.Villages.Messages;


[ProtoContract]
public record ServerVillageChangeState : IEvent
{
    [ProtoMember(1)]
    public Village VillageChange { get;  }

    public ServerVillageChangeState(Village villageChange)
    {
        VillageChange = villageChange;
    }
}
