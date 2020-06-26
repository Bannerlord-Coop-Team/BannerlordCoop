using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class SettlementSerializer : ICustomSerializer
    {
        private string settlementId;
        public SettlementSerializer(Settlement settlement)
        {
            settlementId = settlement.StringId;
        }

        public object Deserialize()
        {
            return Settlement.Find(settlementId);
        }
    }
}