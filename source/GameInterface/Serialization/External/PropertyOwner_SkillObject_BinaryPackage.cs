using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External;

[Serializable]
public class PropertyOwner_SkillObject_BinaryPackage : BinaryPackageBase<PropertyOwner<SkillObject>>
{
    public Dictionary<string, int> SerializedAttributes;

    public PropertyOwner_SkillObject_BinaryPackage(PropertyOwner<SkillObject> obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
    {
    }
    protected override void PackInternal()
    {
        SerializedAttributes = Object._attributes.ToDictionary(x => ResolveId(x.Key), x => x.Value);
    }
    protected override void UnpackInternal()
    {
        Object = new PropertyOwner<SkillObject>();

        foreach (var x in SerializedAttributes)
        {
            Object.SetPropertyValue(ResolveObject<SkillObject>(x.Key), x.Value);
        }
    }
}