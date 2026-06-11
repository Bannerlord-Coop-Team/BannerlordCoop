using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Smithing.Messages;

public record HourTicked : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;

    public HourTicked(CraftingCampaignBehavior craftingCampaignBehavior)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
    }
}

public record DailySettlementTick : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public Settlement Settlement;

    public DailySettlementTick(CraftingCampaignBehavior craftingCampaignBehavior, Settlement settlement)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        Settlement = settlement;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkHourlyTick : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public Dictionary<string, int> HeroIdCraftingRecords;

    public NetworkHourlyTick(string craftingCampaignBehaviorId, Dictionary<string, int> heroIdCraftingRecords)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        HeroIdCraftingRecords = heroIdCraftingRecords;
    }
}