using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using System.Reflection;

namespace MBMultiplayerCampaign.Serializers
{
    [Serializable]
    public class CampaignTimeSerializer : ICustomSerializer
    {
        long numTicks;

        public CampaignTimeSerializer()
        {
        }
        public CampaignTimeSerializer(CampaignTime campaignTime)
        {
            numTicks = (long)typeof(CampaignTime)
                .GetField("_numTicks", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(campaignTime);
        }

        public ICustomSerializer Serialize(object obj)
        {
            return new CampaignTimeSerializer((CampaignTime)obj);
        }

        object ICustomSerializer.Deserialize()
        {
            ConstructorInfo ctorCampaignTime = typeof(CampaignTime).Assembly
                .GetType("TaleWorlds.CampaignSystem.CampaignTime")
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];

            return (CampaignTime)ctorCampaignTime.Invoke(new object[] { numTicks });
        }
    }
}
