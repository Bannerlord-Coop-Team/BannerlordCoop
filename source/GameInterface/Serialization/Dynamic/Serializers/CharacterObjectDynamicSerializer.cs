using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class CharacterObjectDynamicSerializer : IDynamicSerializer
    {
        public CharacterObjectDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<CharacterObject>();
            modelGenerator.CreateDynamicSerializer<BasicCharacterObject>().AddDerivedType<CharacterObject>();
        }
    }
}
