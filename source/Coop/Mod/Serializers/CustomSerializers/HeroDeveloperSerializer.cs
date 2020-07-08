using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class HeroDeveloperSerializer : MBObjectSerializer
    {
        public HeroDeveloperSerializer(HeroDeveloper value) : base(value) { }
    }
}