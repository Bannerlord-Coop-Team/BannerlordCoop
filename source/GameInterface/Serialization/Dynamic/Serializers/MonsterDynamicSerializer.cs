using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class MonsterDynamicSerializer : IDynamicSerializer
    {
        public MonsterDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            string[] exclude = new string[]
            {
                "_monsterMissionData",
            };

            modelGenerator.CreateDynamicSerializer<Monster>(exclude);
        }
    }
}
