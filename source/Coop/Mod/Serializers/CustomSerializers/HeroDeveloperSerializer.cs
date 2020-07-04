using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    internal class HeroDeveloperSerializer : ICustomSerializer
    {
        MBGUID heroDeveloperID;

        public HeroDeveloperSerializer(HeroDeveloper value)
        {
            heroDeveloperID = value.Id;
        }

        public object Deserialize()
        {
            return MBObjectManager.Instance.GetObject(heroDeveloperID);
        }
    }
}