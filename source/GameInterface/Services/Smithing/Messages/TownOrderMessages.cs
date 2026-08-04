using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct TownOrderCreated : IEvent
{
    public readonly Hero OrderOwner;
    public readonly int OrderSlot;

    public TownOrderCreated(Hero orderOwner, int orderSlot)
    {
        OrderOwner = orderOwner;
        OrderSlot = orderSlot;
    }
}

public readonly struct CraftingOrderReplaced : IEvent
{
    public readonly Town Town;
    public readonly int DifficultyLevel;

    public CraftingOrderReplaced(Town town, int difficultyLevel)
    {
        Town = town;
        DifficultyLevel = difficultyLevel;
    }
}

public readonly struct CompleteOrderServer : IEvent
{
    public readonly Town Town;
    public readonly CraftingOrder CraftingOrder;
    public readonly ItemObject CraftedItem;
    public readonly Hero CompleterHero;
    public readonly Hero MainHero;

    public CompleteOrderServer(
        Town town,
        CraftingOrder craftingOrder,
        ItemObject craftedItem,
        Hero completerHero,
        Hero mainHero)
    {
        Town = town;
        CraftingOrder = craftingOrder;
        CraftedItem = craftedItem;
        CompleterHero = completerHero;
        MainHero = mainHero;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCreateTownOrder : ICommand
{
    [ProtoMember(1)]
    public readonly string OrderOwnerId;

    [ProtoMember(2)]
    public readonly string CraftingOrderId;

    [ProtoMember(3)]
    public readonly string RandomElementId; // CraftingTemplateId

    [ProtoMember(4)]
    public readonly int PieceTier;

    [ProtoMember(5)]
    public readonly string NextTownOrderId;

    public NetworkCreateTownOrder(string orderOwnerId, string craftingOrderId, string randomElementId, int pieceTier, string nextTownOrderId)
    {
        OrderOwnerId = orderOwnerId;
        CraftingOrderId = craftingOrderId;
        RandomElementId = randomElementId;
        PieceTier = pieceTier;
        NextTownOrderId = nextTownOrderId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkReplaceCraftingOrder : ICommand
{
    [ProtoMember(1)]
    public readonly string TownId;

    [ProtoMember(2)]
    public readonly int DifficultyLevel;

    public NetworkReplaceCraftingOrder(string townId, int difficultyLevel)
    {
        TownId = townId;
        DifficultyLevel = difficultyLevel;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCompleteOrderClients : ICommand
{
    [ProtoMember(1)]
    public readonly string TownId;

    [ProtoMember(2)]
    public readonly string CraftingOrderId;

    [ProtoMember(3)]
    public readonly string CraftedItemId;

    [ProtoMember(4)]
    public readonly string CompleterHeroId;

    public NetworkCompleteOrderClients(
        string townId,
        string craftingOrderId,
        string craftedItemId,
        string completerHeroId)
    {
        TownId = townId;
        CraftingOrderId = craftingOrderId;
        CraftedItemId = craftedItemId;
        CompleterHeroId = completerHeroId;
    }
}