using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using System.Collections.Generic;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Army
    /// </summary>
    [Serializable]
    public class ArmyBinaryPackage : BinaryPackageBase<Army>
    {

        public ArmyBinaryPackage(Army obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "_hourlyTickEvent",
            "_tickEvent",
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();

            // Resolves _hourlyTickEvent and _tickEvent
            Object.AddEventHandlers();

            // Resolves _armiesCache for Kingdom
            if (Object.Kingdom != null)
            {
                List<Army> kingdomArmies = Object.Kingdom.Armies;
                if (kingdomArmies.Contains(Object) == false)
                {
                    kingdomArmies.Add(Object);
                }
            }
        }
    }
}
