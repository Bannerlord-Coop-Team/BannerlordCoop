using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using System.Reflection;
using Coop.Mod.Extentions;

namespace Coop.Mod.Serializers.Custom
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
                numTicks = campaignTime.GetNumTicks();
            }
        }

        public object Deserialize()
        {
            // only create object if it existed on serialize end
            if (numTicksExists)
            {
                CampaignTime campaignTime = new CampaignTime();
                campaignTime.SetNumTicks(numTicks);
                return campaignTime;
            }
            return null;
        }

        public void ResolveReferenceGuids()
        {
            // No references
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}
