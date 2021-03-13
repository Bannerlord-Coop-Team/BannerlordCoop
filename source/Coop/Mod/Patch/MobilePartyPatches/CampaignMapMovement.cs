using Coop.Mod.Persistence.Party;
using CoopFramework;
using JetBrains.Annotations;
using Sync.Call;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Patch.MobilePartyPatches
{
    /// <summary>
    ///     Handles synchronization of campaign map movement data of a <see cref="MobileParty"/> instance. It records
    ///     all changes made to the relevant fields during one frame. At the end of a frame, the changes are accumulated
    ///     and sent to all clients. Two categories of fields are synchronized this way:
    ///
    ///     1.  Movement order data. See <see cref="MovementData"/>. These are different fields of a party that are
    ///         relevant to it's movement behaviour on the map. Specifically it contains the <see cref="AiBehavior"/>
    ///         that defines the "long term" movement goal and all its related information.
    ///
    ///         The movement order data is buffered for each frame. The buffered data is then passed onto the
    ///         <see cref="MobilePartySync"/> instance which in turn decides what to do with the captured data.
    /// 
    ///     2.  Position data. This is the 2D position on the campaign map of the party. It is captured into a buffer
    ///         on the server only. The positions captured on the server will be sent to all clients. This will overwrite
    ///         whatever the client is doing locally and acts as a ground truth for all parties, regardless of movement
    ///         orders.
    /// </summary>
    public class CampaignMapMovement : CoopManaged<CampaignMapMovement, MobileParty>
    {
        /// <summary>
        ///     Definition of the patch.
        /// </summary>
        static CampaignMapMovement()
        {
            // Define the patched fields for target movement & current position
            MovementOrderGroup =
                new FieldAccessGroup<MobileParty, MovementData>(new FieldAccess[] 
                {
                    Field<AiBehavior>("_defaultBehavior"),
                    Field<Settlement>("_targetSettlement"),
                    Field<MobileParty>("_targetParty"),
                    Field<Vec2>("_targetPosition"),
                    Field<int>("_numberOfFleeingsAtLastTravel")
                });
            MapPosition = Field<Vec2>("_position2D");
            MapPositionSetter = Setter(nameof(MobileParty.Position2D));
            
            Sync = new MobilePartySync(MovementOrderGroup, MapPosition, MapPositionSetter);

            // Synchronize setters for the target movement fields
            When(GameLoop)
                .Changes(MovementOrderGroup)
                .Through(
                    Setter(nameof(MobileParty.DefaultBehavior)),
                    Setter(nameof(MobileParty.TargetSettlement)),
                    Setter(nameof(MobileParty.TargetParty)),
                    Setter(nameof(MobileParty.TargetPosition)))
                .Broadcast(() => Sync);

            // Capture the current position of all parties on the server to a buffer and
            // distribute it to all clients trough `Sync`.
            When(GameLoop & CoopConditions.IsServer)
                .Changes(MapPosition)
                .Through(MapPositionSetter)
                .Broadcast(() => Sync);
            
            // Disabled all party movement calls for parties that are not controlled by this client
            /*When(GameLoop & Not(CoopConditions.ControlsParty))
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
                .Skip();*/

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
                // intact by a separate patch DisablePartyDecisionMaking.
                instance.Ai.SetDoNotMakeNewDecisions(true);
            }
        }

        private static FieldAccessGroup<MobileParty, MovementData> MovementOrderGroup { get; }
        private static FieldAccess<MobileParty, Vec2> MapPosition { get; }
        private static PatchedInvokable MapPositionSetter { get; }
    }
}
