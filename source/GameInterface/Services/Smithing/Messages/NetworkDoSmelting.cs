using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkDoSmelting : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string CraftingHeroId;

    [ProtoMember(3)]
    public string ItemId;

    [ProtoMember(4)]
    public string ItemModifierId;

    [ProtoMember(5)]
    public string CosmeticItemId;

    [ProtoMember(6)]
    public bool IsQuestItem;

    public NetworkDoSmelting(
        string craftingCampaignBehaviorId,
        string craftingHeroId,
        string itemId,
        string itemModifierId,
        string cosmeticItemId,
        bool isQuestItem)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        CraftingHeroId = craftingHeroId;
        ItemId = itemId;
        ItemModifierId = itemModifierId;
        CosmeticItemId = cosmeticItemId;
        IsQuestItem = isQuestItem;
    }
}