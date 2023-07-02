﻿using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for LordPartyComponent
    /// </summary>
    [Serializable]
    public class LordPartyComponentBinaryPackage : BinaryPackageBase<LordPartyComponent>
    {
        public LordPartyComponentBinaryPackage(LordPartyComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "_cachedName",
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();

            // Resolves _warPartyComponentsCache for Kingdom
            Kingdom kingdom = Object.Clan?.Kingdom;
            if (kingdom != null)
            {
                List<WarPartyComponent> kingdomComponents = (List<WarPartyComponent>)KingdomBinaryPackage.Kingdom_WarPartyComponents.GetValue(kingdom);
                if(kingdomComponents == null)
                {
                    KingdomBinaryPackage.InitializeCachedLists.Invoke(kingdom, Array.Empty<object>());
                    kingdomComponents = (List<WarPartyComponent>)KingdomBinaryPackage.Kingdom_WarPartyComponents.GetValue(kingdom);
				}
                
                if (kingdomComponents.Contains(Object) == false)
                {
                    kingdomComponents.Add(Object);
                }
            }
        }
    }
}
