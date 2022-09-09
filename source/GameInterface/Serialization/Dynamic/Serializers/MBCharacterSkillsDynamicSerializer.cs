using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class MBCharacterSkillsDynamicSerializer : IDynamicSerializer
    {
        public MBCharacterSkillsDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<MBCharacterSkills>();
            modelGenerator.CreateDynamicSerializer<CharacterSkills>();
        }
    }
}
