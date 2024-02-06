using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Data.IFactionDatas
{
    [ProtoContract(SkipConstructor = true)]
    public class KingdomFactionData : IFactionData
    {
        [ProtoMember(1)]
        public string KingdomId { get; }

        public KingdomFactionData(string kingdomId)
        {
            KingdomId = kingdomId;
        }

        public override bool TryGetIFaction(IObjectManager objectManager, out IFaction faction)
        {
            if (objectManager.TryGetObject(KingdomId, out Kingdom kingdom))
            {
                faction = kingdom;
                return true;
            }
            else
            {
                faction = null;
                return false;
            }
        }
    }
}
