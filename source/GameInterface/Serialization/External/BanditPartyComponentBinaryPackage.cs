using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BanditPartyComponentBinaryPackage : BinaryPackageBase<BanditPartyComponent>
    {
        public BanditPartyComponentBinaryPackage(BanditPartyComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        static readonly HashSet<string> excludes = new HashSet<string>
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

            try
            {
                Kingdom kingdom = Object.Clan.Kingdom;
                // Resolves _warPartyComponentsCache for Kingdom

                if (kingdom != null)
                {
                    List<WarPartyComponent> kingdomComponents = (List<WarPartyComponent>)KingdomBinaryPackage.Kingdom_WarPartyComponents.GetValue(kingdom);
                    if (kingdomComponents.Contains(Object) == false)
                    {
                        kingdomComponents.Add(Object);
                    }
                }
            }
            catch (NullReferenceException)
            {
                return;
            }
        }
    }
}
