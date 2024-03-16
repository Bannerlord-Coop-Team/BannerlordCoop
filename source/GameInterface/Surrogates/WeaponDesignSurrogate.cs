using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct WeaponDesignSurrogate
{
    [ProtoMember(1)]
    public string WeaponName { get; set; }
    //TODO for now this may work but data may need to be added

    public WeaponDesignSurrogate(WeaponDesign weaponDesign)
    {
        WeaponName = weaponDesign.WeaponName.Value;
    }



    public static implicit operator WeaponDesignSurrogate(WeaponDesign weaponDesign)
    {
        return new WeaponDesignSurrogate(weaponDesign);
    }

    public static implicit operator WeaponDesign(WeaponDesignSurrogate surrogate)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return null;


        // may not work
        if (objectManager.TryGetObject<WeaponDesign>(surrogate.WeaponName, out var weaponDesign) == false) return null;

        return weaponDesign;
    }
}
