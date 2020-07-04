using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    internal class BasicTroopSerializer : ICustomSerializer
    {
        MBGUID MBGUID;

        public BasicTroopSerializer(CharacterObject value)
        {
            MBGUID = value.Id;
        }

        public object Deserialize()
        {
            return MBObjectManager.Instance.GetObject(MBGUID);
        }
    }
}