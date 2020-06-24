using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class ClanSerializer : ICustomSerializer
    {

        public ClanSerializer(Clan clan)
        {
        }

        public object Deserialize()
        {
            throw new NotImplementedException();
        }
    }
}
