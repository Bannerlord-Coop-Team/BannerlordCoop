using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Data.IFactionDatas
{
    [ProtoContract(SkipConstructor = true)]
    public class ClanFactionData : IFactionData
    {

        [ProtoMember(1)]
        public string ClanId { get; }

        public ClanFactionData(string clanId)
        {
            ClanId = clanId;
        }

        public override bool TryGetIFaction(IObjectManager objectManager, out IFaction faction)
        {
            if (objectManager.TryGetObject(ClanId, out Clan clan))
            {
                faction = clan;
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
