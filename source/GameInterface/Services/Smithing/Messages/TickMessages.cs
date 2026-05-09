using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Smithing.Messages;

public record HourTicked : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;

    public HourTicked(CraftingCampaignBehavior craftingCampaignBehavior)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkHourlyTickServer : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    public NetworkHourlyTickServer(string craftingCampaignBehaviorId)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkHourlyTickClients : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    public NetworkHourlyTickClients(NetworkHourlyTickServer cloneObject)
    {
        CraftingCampaignBehaviorId = cloneObject.CraftingCampaignBehaviorId;
    }
}