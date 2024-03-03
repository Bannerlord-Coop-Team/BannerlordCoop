using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for CharacterObject
    /// </summary>
    [Serializable]
    public class CharacterObjectBinaryPackage : BinaryPackageBase<CharacterObject>
    {

        string stringId;

        string battleEquipmentTemplateId;
        string civilianEquipmentTemplateId;
        string originCharacterId;
        string[] UpgradeTargetIds;

        public CharacterObjectBinaryPackage(CharacterObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public static readonly HashSet<string> Excludes = new HashSet<string>
        {
            "_battleEquipmentTemplate",
            "_civilianEquipmentTemplate",
            "_originCharacter",
            "<UpgradeTargets>k__BackingField",
        };

        protected override void PackInternal()
        {
            stringId = Object.StringId ?? string.Empty;

            base.PackFields(Excludes);

            // Get the value of the CharacterObject_battleEquipmentTemplate field in the object
            CharacterObject battleEquipmentTemplate = Object._battleEquipmentTemplate;
            battleEquipmentTemplateId = battleEquipmentTemplate?.StringId;

            // Get the value of the CharacterObject_civilianEquipmentTemplate field in the object
            CharacterObject civilianEquipmentTemplate = Object._civilianEquipmentTemplate;
            civilianEquipmentTemplateId = civilianEquipmentTemplate?.StringId;

            // Get the value of the CharacterObject_originCharacter field in the object
            CharacterObject originCharacter = Object._originCharacter;
            originCharacterId = originCharacter?.StringId;

            // Store the result of calling the PackIds method on the object's UpgradeTargets property in the UpgradeTargetIds variable
            UpgradeTargetIds = PackIds(Object.UpgradeTargets);
        }

        protected override void UnpackInternal()
        {
            CharacterObject characterObject = ResolveId<CharacterObject>(stringId);
            if (characterObject != null)
            {
                Object = characterObject;
                return;
            }

            base.UnpackFields();

            // Resolve Ids for StringId resolvable objects
            Object._battleEquipmentTemplate = ResolveId<CharacterObject>(battleEquipmentTemplateId);
            Object._civilianEquipmentTemplate = ResolveId<CharacterObject>(civilianEquipmentTemplateId);
            Object._originCharacter = ResolveId<CharacterObject>(originCharacterId);

            Object.UpgradeTargets = ResolveIds<CharacterObject>(UpgradeTargetIds).ToArray();
        }
    }
}