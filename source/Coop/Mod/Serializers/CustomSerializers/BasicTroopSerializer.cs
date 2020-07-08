using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class BasicTroopSerializer : ICustomSerializer
    {
        MBGUIDSerializer id;

        public BasicTroopSerializer(CharacterObject value)
        {
            id = new MBGUIDSerializer(value.Id);
        }

        public object Deserialize()
        {
            return MBObjectManager.Instance.GetObject((MBGUID)id.Deserialize());
        }
    }
}