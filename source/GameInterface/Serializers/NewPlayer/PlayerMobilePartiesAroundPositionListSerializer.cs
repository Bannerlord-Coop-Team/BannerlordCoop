using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class PlayerMobilePartiesAroundPositionListSerializer : ICustomSerializer
    {
        private List<string> partyNames = new List<string>();

        public PlayerMobilePartiesAroundPositionListSerializer(MobilePartiesAroundPositionList mobilePartiesAroundPositionList)
        {
            List<MobileParty> partyList = (List<MobileParty>)typeof(MobilePartiesAroundPositionList)
                .GetField("_partyList", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(mobilePartiesAroundPositionList);

            foreach (MobileParty mobileParty in partyList)
            {
                partyNames.Add(mobileParty.Name.ToString());
            }
        }

        public object Deserialize()
        {
            MobilePartiesAroundPositionList newPositionList = new MobilePartiesAroundPositionList(partyNames.Count > 32 ? partyNames.Count : 32);
            List<MobileParty> partyList = (List<MobileParty>)typeof(MobilePartiesAroundPositionList)
                .GetField("_partyList", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(newPositionList);
            foreach (MobileParty mobileParty in MobileParty.All)
            {
                if (partyNames.Contains(mobileParty.Name.ToString()))
                {
                    partyNames.Remove(mobileParty.Name.ToString());
                    partyList.Add(mobileParty);
                }
            }

            return newPositionList;
        }

        public void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}