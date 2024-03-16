using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemComponentSurrogate
{
    [ProtoMember(1)]
    public string StringId { get; set; }
    [ProtoMember(2)]
    public string ItemStringId { get; set; }
    [ProtoMember(3)]
    public ItemModifierGroup ItemModifierGroup { get; set; }

    public ItemComponentSurrogate(ItemComponent component)
    {
        StringId = component.StringId;
        ItemStringId = component.Item.StringId;


        // ItemModifierGroupSurrogate

        ItemModifierGroup = component.ItemModifierGroup;
    }

    public static implicit operator ItemComponentSurrogate(ItemComponent component)
    {
        return new ItemComponentSurrogate(component);
    }

    public static implicit operator ItemComponent(ItemComponentSurrogate surrogate)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return null;

        if (objectManager.TryGetObject<ItemComponent>(surrogate.StringId, out var component) == false) return null;

        if (objectManager.TryGetObject<ItemObject>(surrogate.ItemStringId, out var itemObject) == false) return null;

        component.Item = itemObject;
        return component;
    }
}
