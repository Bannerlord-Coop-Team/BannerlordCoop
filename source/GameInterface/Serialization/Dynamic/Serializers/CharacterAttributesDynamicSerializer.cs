using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    public class CharacterAttributesDynamicSerializer : IDynamicSerializer
    {
        public CharacterAttributesDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<CharacterAttributes>();

            // NOTE: CharacterAttribute is serialized via string id
        }
    }
}
