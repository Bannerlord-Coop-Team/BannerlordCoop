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
            if(settlement != null)
            {
                settlementId = settlement.StringId;
            }
            
        }

        public object Deserialize()
        {
            if(settlementId != null)
            {
                return Settlement.Find(settlementId);
            }

            return null;
        }
    }
}