using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers.Custom
{
    /// <summary>
    /// Used as a part of the ClanSerializer to break down the PartyTemplateObject field
    /// </summary>
    [Serializable]
    public class DefaultPartyTemplateSerializer : ICustomSerializer
    {
        private string partyTemplateId;
        public DefaultPartyTemplateSerializer(PartyTemplateObject partyTemplate)
        {

            if (partyTemplate != null)
            {
                partyTemplateId = partyTemplate.StringId;
            }
        }

        public object Deserialize()
        {
            if (partyTemplateId != null)
            {
                return Campaign.Current.CurrentGame.ObjectManager.GetObject<PartyTemplateObject>(partyTemplateId);
            }

            return null;
        }

        public void ResolveReferenceGuids()
        {
            // No references
        }
    }
}
