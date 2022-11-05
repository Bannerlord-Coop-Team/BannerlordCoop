using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class TroopRosterElementSerializer : CustomSerializer
    {

        Guid characterObjectGuid;

        public TroopRosterElementSerializer(TroopRosterElement rosterElement) : base(rosterElement)
        {
            characterObjectGuid = CoopObjectManager.GetGuid(rosterElement.Character);
        }

        public override object Deserialize()
        {
            throw new NotImplementedException();
        }

        public override void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This is needed to manage Structs as they do not allow references in enumarables
        /// </summary>
        /// <returns>New RosterElement with deserialized and referenced objects</returns>
        public TroopRosterElement UnpackObject()
        {
            TroopRosterElement rosterElement = new TroopRosterElement();

            foreach (FieldInfo field in SerializableObjects.Keys)
            {
                field.SetValue(rosterElement, SerializableObjects[field]);
            }

            rosterElement.Character = CoopObjectManager.GetObject<CharacterObject>(characterObjectGuid);

            return (TroopRosterElement)base.Deserialize(rosterElement);
        }
    }
}
