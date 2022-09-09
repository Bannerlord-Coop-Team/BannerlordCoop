using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class BodyPropertyDynamicSerializer : IDynamicSerializer
    {
        public BodyPropertyDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<BodyProperties>();
            modelGenerator.CreateDynamicSerializer<DynamicBodyProperties>();
            modelGenerator.CreateDynamicSerializer<StaticBodyProperties>();
        }
    }
}
