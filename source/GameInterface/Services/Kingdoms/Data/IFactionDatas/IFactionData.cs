using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Data.IFactionDatas
{
    [ProtoContract(SkipConstructor = true)]
    [ProtoInclude(1, typeof(ClanFactionData))]
    [ProtoInclude(2, typeof(KingdomFactionData))]
    public abstract class IFactionData
    {
        public abstract bool TryGetIFaction(IObjectManager objectManager, out IFaction faction);
    }
}
