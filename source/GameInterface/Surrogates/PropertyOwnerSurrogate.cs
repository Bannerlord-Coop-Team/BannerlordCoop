using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct PropertyOwnerSurrogate
{
    [ProtoMember(1)]
    public Dictionary<string, int> Attributes { get; set; }

    public PropertyOwnerSurrogate(PropertyOwner<TraitObject> owner)
    {
        Attributes = new Dictionary<string, int>();
        if (owner == null)
            return;
        foreach (var property in owner.GetProperties())
        {
            Attributes[property.StringId] = owner.GetPropertyValue(property);
        }
    }

    public static implicit operator PropertyOwnerSurrogate(PropertyOwner<TraitObject> owner)
    {
        return new PropertyOwnerSurrogate(owner);
    }

    public static implicit operator PropertyOwner<TraitObject>(PropertyOwnerSurrogate surrogate)
    {
        var owner = new PropertyOwner<TraitObject>();
        foreach (var kvp in surrogate.Attributes)
        {
            var obj = MBObjectManager.Instance.GetObject<TraitObject>(kvp.Key);
            if (obj != null)
            {
                owner.SetPropertyValue(obj, kvp.Value);
            }
        }
        return owner;
    }
}