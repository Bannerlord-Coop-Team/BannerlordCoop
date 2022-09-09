using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    public class CharacterPerksDynamicSerializer : IDynamicSerializer
    {
        public CharacterPerksDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<CharacterPerks>();

            // NOTE: CharacterPerk is serialized via string id
        }
    }
}
