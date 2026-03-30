using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External;

[Serializable]
public class PropertyOwner_TraitObject_BinaryPackage : BinaryPackageBase<PropertyOwner<TraitObject>>
{
    public Dictionary<string, int> SerializedAttributes;

    public PropertyOwner_TraitObject_BinaryPackage(PropertyOwner<TraitObject> obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
    {
    }
    protected override void PackInternal()
    {
        SerializedAttributes = Object._attributes.ToDictionary(x => ResolveId(x.Key), x => x.Value);
    }
    protected override void UnpackInternal()
    {
        Object = new PropertyOwner<TraitObject>();

        foreach (var x in SerializedAttributes)
        {
            Object.SetPropertyValue(ResolveObject<TraitObject>(x.Key), x.Value);
        }
    }
}