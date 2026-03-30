using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External;

[Serializable]
public class PropertyOwner_CharacterAttribute_BinaryPackage : BinaryPackageBase<PropertyOwner<CharacterAttribute>>
{
    public Dictionary<string, int> SerializedAttributes;

    public PropertyOwner_CharacterAttribute_BinaryPackage(PropertyOwner<CharacterAttribute> obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
    {
    }
    protected override void PackInternal()
    {
        SerializedAttributes = Object._attributes.ToDictionary(x => ResolveId(x.Key), x => x.Value);
    }
    protected override void UnpackInternal()
    {
        Object = new PropertyOwner<CharacterAttribute>();

        foreach (var x in SerializedAttributes)
        {
            Object.SetPropertyValue(ResolveObject<CharacterAttribute>(x.Key), x.Value);
        }
    }
}