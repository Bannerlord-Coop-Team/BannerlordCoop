using Coop.Mod.Persistence.Party;
using CoopFramework;
using JetBrains.Annotations;
using Sync.Value;
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
        ///     Definition of the patch.
        /// </summary>
        static CampaignMapMovement()
        {
            // The fields relevant for party movement
            Movement = 
                new FieldAccessGroup<MobileParty, MovementData>(new FieldAccess[]
                {
                    Field<AiBehavior>("_defaultBehavior"),
                    Field<Settlement>("_targetSettlement"),
                    Field<MobileParty>("_targetParty"),
                    Field<Vec2>("_targetPosition"),
                    Field<int>("_numberOfFleeingsAtLastTravel")
                });
            Sync = new MobilePartySync(Movement);

            // Setters for the movement fields
            When(GameLoop)
                .Changes(Movement)
                .Through(
                    Setter(nameof(MobileParty.DefaultBehavior)),
                    Setter(nameof(MobileParty.TargetSettlement)),
                    Setter(nameof(MobileParty.TargetParty)),
                    Setter(nameof(MobileParty.TargetPosition)))
                .Broadcast(() => Sync);
            
            When(GameLoop & Not(CoopConditions.ControlsParty))
                .Calls(
                    Method(nameof(MobileParty.SetMoveBesiegeSettlement)),
                    Method(nameof(MobileParty.SetMoveDefendSettlement)),
                    Method(nameof(MobileParty.SetMoveEngageParty)),
                    Method(nameof(MobileParty.SetMoveEscortParty)),
                    Method(nameof(MobileParty.SetMoveModeHold)),
                    Method(nameof(MobileParty.SetMoveRaidSettlement)),
                    Method(nameof(MobileParty.SetMoveGoAroundParty)),
                    Method(nameof(MobileParty.SetMoveGoToPoint)),
                    Method(nameof(MobileParty.SetMoveGoToSettlement)),
                    Method(nameof(MobileParty.SetMovePatrolAroundPoint)),
                    Method(nameof(MobileParty.SetMovePatrolAroundSettlement))
                    )
                .Skip();

            AutoWrapAllInstances(party => new CampaignMapMovement(party));
        }
        
        /// <summary>
        ///     Synchronization instance for all movement data.
        /// </summary>
        public static MobilePartySync Sync { get; }

        public CampaignMapMovement([NotNull] MobileParty instance) : base(instance)
        {
            if (!Coop.IsController(instance))
            {
                // Disable AI decision making for newly spawned parties that we do not control locally. Will be kept
                // intact by a separate patch DisablePartyAi.
                instance.Ai.SetDoNotMakeNewDecisions(true);
            }
        }

        private static FieldAccessGroup<MobileParty, MovementData> Movement { get; }
    }
}
