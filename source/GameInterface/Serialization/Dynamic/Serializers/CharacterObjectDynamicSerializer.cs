using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class CharacterObjectDynamicSerializer : IDynamicSerializer
    {
        public CharacterObjectDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            var excluded = new string[]
            {
            };

            modelGenerator.CreateDynamicSerializer<Hero>(excluded);
        }
    }
}
