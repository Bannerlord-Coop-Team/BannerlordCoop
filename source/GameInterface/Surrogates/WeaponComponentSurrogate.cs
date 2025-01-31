using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;


[ProtoContract]
internal struct WeaponComponentSurrogate
{
    [ProtoMember(1)]
    public ItemObject Item { get; set; }
    [ProtoMember(2)]
    public ItemModifierGroup ItemModifierGroup { get; set; }


    public WeaponComponentSurrogate(WeaponComponent weaponComponent)
    {
        if (weaponComponent == null)
        {
            Item = ObjectHelper.SkipConstructor<ItemObject>();
            ItemModifierGroup = ObjectHelper.SkipConstructor<ItemModifierGroup>();
        }
        else
        {
            Item = weaponComponent.Item;
            ItemModifierGroup = weaponComponent.ItemModifierGroup;
        }
    }

    public static implicit operator WeaponComponentSurrogate(WeaponComponent weaponComponent)
    {
        return new WeaponComponentSurrogate(weaponComponent);
    }

    public static implicit operator WeaponComponent(WeaponComponentSurrogate surrogate)
    {
        return new WeaponComponent(surrogate.Item);
    }
}
