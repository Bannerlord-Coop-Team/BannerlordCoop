using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    public class CharacterTraitsDynamicSerializer : IDynamicSerializer
    {
        public CharacterTraitsDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            var excluded = new string[]
            {
            };

            modelGenerator.CreateDynamicSerializer<CharacterTraits>(excluded);
        }
    }
}
