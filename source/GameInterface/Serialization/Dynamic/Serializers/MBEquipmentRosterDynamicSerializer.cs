using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class MBEquipmentRosterDynamicSerializer : IDynamicSerializer
    {
        public MBEquipmentRosterDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<MBEquipmentRoster>();
            modelGenerator.CreateDynamicSerializer<Equipment>();
            modelGenerator.CreateDynamicSerializer<EquipmentElement>();
        }
    }
}
