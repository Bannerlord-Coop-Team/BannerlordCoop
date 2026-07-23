using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public record TownOrderCreated : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public Hero OrderOwner;
    public int OrderSlot;

    public TownOrderCreated(CraftingCampaignBehavior craftingCampaignBehavior, Hero orderOwner, int orderSlot)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        OrderOwner = orderOwner;
        OrderSlot = orderSlot;
    }
}

public record CraftingOrderReplaced : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public Town Town;
    public int DifficultyLevel;

    public CraftingOrderReplaced(CraftingCampaignBehavior craftingCampaignBehavior, Town town, int difficultyLevel)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        Town = town;
        DifficultyLevel = difficultyLevel;
    }
}

public record OrderCompleted : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public Town Town;
    public CraftingOrder CraftingOrder;
    public ItemObject CraftedItem;
    public Hero CompleterHero;
    public Hero MainHero;
    public bool Flag;

    public OrderCompleted(CraftingCampaignBehavior craftingCampaignBehavior, Town town, CraftingOrder craftingOrder, ItemObject craftedItem, Hero completerHero, Hero mainHero, bool flag)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        Town = town;
        CraftingOrder = craftingOrder;
        CraftedItem = craftedItem;
        CompleterHero = completerHero;
        MainHero = mainHero;
        Flag = flag;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkCreateTownOrder : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string OrderOwnerId;

    [ProtoMember(3)]
    public string CraftingOrderId;

    [ProtoMember(4)]
    public string RandomElementId; // CraftingTemplateId

    [ProtoMember(5)]
    public int PieceTier;

    [ProtoMember(6)]
    public string NextTownOrderId;

    public NetworkCreateTownOrder(string craftingCampaignBehaviorId, string orderOwnerId, string craftingOrderId, string randomElementId, int pieceTier, string nextTownOrderId)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        OrderOwnerId = orderOwnerId;
        CraftingOrderId = craftingOrderId;
        RandomElementId = randomElementId;
        PieceTier = pieceTier;
        NextTownOrderId = nextTownOrderId;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkReplaceCraftingOrder : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string TownId;

    [ProtoMember(3)]
    public int DifficultyLevel;

    public NetworkReplaceCraftingOrder(string craftingCampaignBehaviorId, string townId, int difficultyLevel)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        TownId = townId;
        DifficultyLevel = difficultyLevel;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkCompleteOrderServer : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string TownId;

    [ProtoMember(3)]
    public string CraftingOrderId;

    [ProtoMember(4)]
    public string CraftedItemId;

    [ProtoMember(5)]
    public string CompleterHeroId;

    [ProtoMember(6)]
    public string MainHeroId;

    [ProtoMember(7)]
    public bool Flag;

    public NetworkCompleteOrderServer(string craftingCampaignBehaviorId, string townId, string craftingOrderId, string craftedItemId, string completerHeroId, string mainHeroId, bool flag)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        TownId = townId;
        CraftingOrderId = craftingOrderId;
        CraftedItemId = craftedItemId;
        CompleterHeroId = completerHeroId;
        MainHeroId = mainHeroId;
        Flag = flag;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkCompleteOrderClients : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string TownId;

    [ProtoMember(3)]
    public string CraftingOrderId;

    [ProtoMember(4)]
    public string CraftedItemId;

    [ProtoMember(5)]
    public string CompleterHeroId;

    public NetworkCompleteOrderClients(NetworkCompleteOrderServer cloneObject)
    {
        CraftingCampaignBehaviorId = cloneObject.CraftingCampaignBehaviorId;
        TownId = cloneObject.TownId;
        CraftingOrderId = cloneObject.CraftingOrderId;
        CraftedItemId = cloneObject.CraftedItemId;
        CompleterHeroId = cloneObject.CompleterHeroId;
    }
}