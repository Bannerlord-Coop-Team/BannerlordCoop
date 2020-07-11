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
        public CampaignTimeSerializer(CampaignTime campaignTime)
        {
            // long cannot be null so a flag is needed for null campaign time
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
            // only create object if it existed on serialize end
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
