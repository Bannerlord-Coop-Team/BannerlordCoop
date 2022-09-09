using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    public class MBBodyPropertiesDynamicSerializer : IDynamicSerializer
    {
        public MBBodyPropertiesDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<MBBodyProperty>();
            modelGenerator.CreateDynamicSerializer<BodyProperties>();
            modelGenerator.CreateDynamicSerializer<DynamicBodyProperties>();
            modelGenerator.CreateDynamicSerializer<StaticBodyProperties>();
        }
    }
}
