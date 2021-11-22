using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class VillageSerializer : ICustomSerializer
    {
        private string villageId;
        public VillageSerializer(Village village)
        {
            if(villageId != null)
            {
                villageId = village.StringId;
            }
            
        }

        public object Deserialize()
        {
            if(villageId != null)
            {
                return Settlement.Find(villageId);
            }

            return null;
        }

        public void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}