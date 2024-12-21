using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.WeaponDesigns.Messages.Collection;

/// <summary>
/// Command to remove a besieger party from <see cref="Crafting._history"/>
/// </summary>
/// 
[ProtoContract(SkipConstructor = true)]
public record NetworkRemoveWeaponDesign : ICommand
{
    public NetworkRemoveWeaponDesign(WeaponDesignData weaponDesignData)
    {
        CraftingId = weaponDesignData.CraftingId;
        WeaponDesignId = weaponDesignData.WeaponDesignId;
    }

    [ProtoMember(1)]
    public string CraftingId { get; }
    [ProtoMember(2)]
    public string WeaponDesignId { get; }
}