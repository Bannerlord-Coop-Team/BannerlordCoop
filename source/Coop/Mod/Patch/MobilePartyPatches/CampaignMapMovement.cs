using Coop.Mod.Persistence.Party;
using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Patch.MobilePartyPatches
{
    /// <summary>
    ///     Intercepts changes to all movement data of a <see cref="MobileParty"/> instance. The synchronization
    ///     of the intercepted data is handled through <see cref="MobilePartySync"/>.
    /// </summary>
    public class CampaignMapMovement : CoopManaged<CampaignMapMovement, MobileParty>
    {
        /// <summary>
        ///     Condition that returns whether the <see cref="MobileParty"/> instance is controlled by the local
        ///     game instance.
        /// </summary>
        private static Condition PartyController = new Condition((eOriginator, instance) =>
        {
            return eOriginator == EOriginator.Game && Coop.IsController(instance as MobileParty);
        });
        
        /// <summary>
        ///     Definition of the patch.
        /// </summary>
        static CampaignMapMovement()
        {
            When(PartyController)
                .Changes(
                    Field<AiBehavior>("_defaultBehavior"),
                    Field<Settlement>("_targetSettlement"),
                    Field<MobileParty>("_targetParty"),
                    Field<Vec2>("_targetPosition"),
                    Field<int>("_numberOfFleeingsAtLastTravel"))
                .Through(
                    Setter(nameof(MobileParty.DefaultBehavior)),
                    Setter(nameof(MobileParty.TargetSettlement)),
                    Setter(nameof(MobileParty.TargetParty)),
                    Setter(nameof(MobileParty.TargetPosition)))
                .Broadcast(() => Sync)
                .Keep(); // Keep the local changes as a preview.
            
            AutoWrapAllInstances(party => new CampaignMapMovement(party));
        }
        
        /// <summary>
        ///     Synchronization instance for all movement data.
        /// </summary>
        public static MobilePartySync Sync { get; } = new MobilePartySync();

        public CampaignMapMovement([NotNull] MobileParty instance) : base(instance)
        {
        }
    }
}
