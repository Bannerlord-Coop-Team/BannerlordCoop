using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Extentions
{
    public static class CampaignTimeExtensions
    {
        static readonly FieldInfo _ticksField = typeof(CampaignTime).GetField("_numTicks", BindingFlags.NonPublic | BindingFlags.Instance);
        public static long GetNumTicks(this CampaignTime campaignTime)
        {
            return (long)_ticksField.GetValue(campaignTime);
        }

        public static void SetNumTicks(this CampaignTime campaignTime, long ticks)
        {
            _ticksField.SetValue(campaignTime, ticks);
        }
    }
}
