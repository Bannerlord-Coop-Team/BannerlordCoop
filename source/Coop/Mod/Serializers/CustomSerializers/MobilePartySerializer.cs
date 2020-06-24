using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class MobilePartySerializer : CustomSerializer
    {
        //PartySerializer party;
        public MobilePartySerializer(MobileParty mobileParty) : base(mobileParty)
        {
            int i = NonSerializableObjects.Count;
        }
        public override object Deserialize()
        {
            MobileParty clientParty = MBObjectManager.Instance.CreateObject<MobileParty>("");
            clientParty.SetAsMainParty();
            throw new NotImplementedException();
        }
    }
}
