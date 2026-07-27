using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkBehaviorSetCraftedWeaponNameServer : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string CraftedWeaponId;

    [ProtoMember(3)]
    public TextObject Name;

    public NetworkBehaviorSetCraftedWeaponNameServer(
        string craftingCampaignBehaviorId,
        string craftedWeaponId,
        TextObject name)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        CraftedWeaponId = craftedWeaponId;
        Name = name;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkBehaviorSetCraftedWeaponNameClients : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string CraftedWeaponId;

    [ProtoMember(3)]
    public TextObject Name;

    public NetworkBehaviorSetCraftedWeaponNameClients(NetworkBehaviorSetCraftedWeaponNameServer cloneObject)
    {
        CraftingCampaignBehaviorId = cloneObject.CraftingCampaignBehaviorId;
        CraftedWeaponId = cloneObject.CraftedWeaponId;
        Name = cloneObject.Name;
    }
}