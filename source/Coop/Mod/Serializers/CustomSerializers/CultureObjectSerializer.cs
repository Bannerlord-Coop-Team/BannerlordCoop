using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class CultureObjectSerializer : ICustomSerializer
    {
        MBGUID cultureId;
        public CultureObjectSerializer(CultureObject culture)
        {
            cultureId = culture.Id;
        }

        public object Deserialize()
        {
            return MBObjectManager.Instance.GetObject(cultureId);
        }
    }
}