using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkSetHeroCraftingStamina : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string CraftingHeroId;

    [ProtoMember(3)]
    public int Value;

    public NetworkSetHeroCraftingStamina(string craftingCampaignBehaviorId, string craftingHeroId, int value)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        CraftingHeroId = craftingHeroId;
        Value = value;
    }
}