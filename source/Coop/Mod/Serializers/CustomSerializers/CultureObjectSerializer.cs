using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class CultureObjectSerializer : MBObjectSerializer
    {
        public CultureObjectSerializer(CultureObject culture) : base(culture) { }
    }
}