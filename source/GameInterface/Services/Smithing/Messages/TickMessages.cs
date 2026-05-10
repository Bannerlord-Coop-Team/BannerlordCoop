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
public class NetworkHourlyTick : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    public NetworkHourlyTick(string craftingCampaignBehaviorId)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
    }
}