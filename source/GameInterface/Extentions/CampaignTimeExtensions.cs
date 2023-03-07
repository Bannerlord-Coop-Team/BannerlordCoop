using System.Reflection;
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
            _ticksField.SetValueDirect(__makeref(campaignTime), ticks);
        }
    }
}
