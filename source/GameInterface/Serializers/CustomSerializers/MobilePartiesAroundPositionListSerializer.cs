using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class MobilePartiesAroundPositionListSerializer : ICustomSerializer
    {
        [NonSerialized]
        MobilePartiesAroundPositionList partiesList;

        private List<Guid> parties;

        public MobilePartiesAroundPositionListSerializer(MobilePartiesAroundPositionList mobilePartiesAroundPositionList)
        {
            List<MobileParty> partyList = (List<MobileParty>)typeof(MobilePartiesAroundPositionList)
                .GetField("_partyList", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(mobilePartiesAroundPositionList);

            parties = new List<Guid>(CoopObjectManager.GetGuids(partyList));
        }

        public object Deserialize()
        {
            partiesList = new MobilePartiesAroundPositionList();
            return partiesList;
        }

        public void ResolveReferenceGuids()
        {
            if (partiesList == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            List<MobileParty> partiesAroundParty = parties.Select(x => (MobileParty)CoopObjectManager.GetObject(x)).ToList();

            typeof(MobilePartiesAroundPositionList)
                .GetField("_partyList", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(partiesList, partiesAroundParty);
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}