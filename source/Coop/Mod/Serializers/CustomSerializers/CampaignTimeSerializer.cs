using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using System.Reflection;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class CampaignTimeSerializer : ICustomSerializer
    {
        long numTicks;
        bool numTicksExists;
        public CampaignTimeSerializer()
        {
        }
        public CampaignTimeSerializer(CampaignTime campaignTime)
        {
            numTicksExists = campaignTime != null;
            if (numTicksExists)
            {
                numTicks = (long)typeof(CampaignTime)
                .GetField("_numTicks", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(campaignTime);
            }
        }

        public object Deserialize()
        {
            if (numTicksExists)
            {
                ConstructorInfo ctorCampaignTime = typeof(CampaignTime).Assembly
                    .GetType("TaleWorlds.CampaignSystem.CampaignTime")
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];

                return (CampaignTime)ctorCampaignTime.Invoke(new object[] { numTicks });
            }
            return null;
        }
    }
}
