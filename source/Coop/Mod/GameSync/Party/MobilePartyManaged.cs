using Common;
using Coop.Mod.Persistence.Party;
using CoopFramework;
using JetBrains.Annotations;
using NLog;
using Sync.Behaviour;
using Sync.Call;
using Sync.Store;
using Sync.Value;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.GameSync.Party
{
    /// <summary>
    ///     Attachment class that is instantiated for each <see cref="MobileParty"/> on both the server and all clients. 
    ///     It is responsible to manage the state of the <see cref="MobileParty"/> such that it stays in sync across all 
    ///     clients.
    /// </summary>
    /// <remarks>
    ///     It currently handles:
    ///     
    ///     - Instance lifetime:
    ///       Automatically creates & destroys instances such that each <see cref="MobileParty"/> such that there is always 
    ///       a corresponding instance.
    ///       
    ///     - Instance synchronization: 
    ///       Whenever a <see cref="MobileParty"/> is created on the server, it is broadcast to all clients.
    ///       
    ///     - Railgun entity management:
    ///       Makes sure all <see cref="MobileParty"/> are represented in Railgun, <see cref="Persistence.MobilePartyEntityManager"/>.
    ///       
    ///     - Campaign map position recording:
    ///       Creates the necessary patches to record and buffer all changes to the map position of all parties that are 
    ///       controlled by this game instance. At the end of a frame, all changes are handed off the Railgun for synchronization 
    ///       to remote game instances.
    ///       
    ///     - Campaign map position overwrite:
    ///       Creates the necessary patches to apply the authoritative campaign map position as received through Railgun.
    /// </remarks>
    class MobilePartyManaged : CoopManaged<MobilePartyManaged, MobileParty>, IDisposable
    {
        /// <summary>
        ///     Generates the necessary patches.
        /// </summary>
        static MobilePartyManaged()
        {
            // Setup events in order to create a `MobilePartyManaged` instance whenever a party is spawned.
            var onNewMobilePartyMethod = typeof(MobilePartyManaged).GetMethod(nameof(MobilePartyManaged.OnNewMobileParty), BindingFlags.NonPublic | BindingFlags.Static);
            OnNewMobilePartyRPC = new Invokable(onNewMobilePartyMethod, EInvokableFlag.TransferArgumentsByValue); // By value because we actually want to serialize the m
            ApplyStaticPatches();

            // Generate patches for map movement sync            
            MovementPatches = new MapMovementPatches
            {
                MovementOrderGroup = new FieldAccessGroup<MobileParty, MovementData>(new FieldAccess[]
                {
                    Field<AiBehavior>("_defaultBehavior"),
                    Field<Settlement>("_targetSettlement"),
                    Field<MobileParty>("_targetParty"),
                    Field<Vec2>("_targetPosition"),
                    Field<int>("_numberOfFleeingsAtLastTravel")
                }),
                MapPosition = Field<Vec2>("_position2D"),
                MapPositionSetter = Setter(nameof(MobileParty.Position2D)),
                DefaultBehaviourSetter = Setter(nameof(MobileParty.DefaultBehavior)),
                TargetSettlementSetter = Setter(nameof(MobileParty.TargetSettlement)),
                TargetPartySetter = Setter(nameof(MobileParty.TargetParty)),
                TargetPositionSetter = Setter(nameof(MobileParty.TargetPosition))
            };
            MovementSync = new MobilePartyMovementSync(MovementPatches.MovementOrderGroup, MovementPatches.MapPosition);

            // On clients, send the movement orders for our party to the server
            When(GameLoop & CoopConditions.IsRemoteClient & CoopConditions.ControlsParty)
                .Changes(MovementPatches.MovementOrderGroup)
                .Through(
                    MovementPatches.DefaultBehaviourSetter,
                    MovementPatches.TargetSettlementSetter,
                    MovementPatches.TargetPartySetter,
                    MovementPatches.TargetPositionSetter)
                .Broadcast(() => MovementSync);
            // Movement orders are only applied on the server and for the controlled parties.
            When(GameLoop & !CoopConditions.ControlsParty)
                .Changes(MovementPatches.MovementOrderGroup)
                .Through(
                    MovementPatches.DefaultBehaviourSetter,
                    MovementPatches.TargetSettlementSetter,
                    MovementPatches.TargetPartySetter,
                    MovementPatches.TargetPositionSetter)
                .Revert();
        }

        #region Liftetime management
        /// <summary>
        ///     Constructor called exactly once for every <see cref="MobileParty"/> instance created.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="doSyncCreation">
        ///     True to send the party to all clients before adding as a managed railgun entity.
        ///     False will add the party to railgun immediately. Warning: Using False in a game with connected remotes will desync! This is only intended to be used directly after loading.
        /// </param>
        private MobilePartyManaged([NotNull] MobileParty party, bool doSyncCreation = true) : base(party)
        {
            Guid guid = CoopObjectManager.AddObject(party);
            if (Coop.IsServer)
            {
                if(doSyncCreation)
                {
                    // Send the new party to all remote clients
                    CoopServer.Instance.Synchronization.Broadcast(OnNewMobilePartyRPC.Id, null, new object[] { party, guid });
                }
                else
                {
                    OnNewMobileParty(party, guid);
                }
            }

            if (!Coop.IsController(party))
            {
                // Disable AI decision making for newly spawned parties that we do not control locally. Will be kept
                // intact by a separate patch DisablePartyDecisionMaking.
                party.Ai.SetDoNotMakeNewDecisions(true);
            }
        }

        /// <summary>
        ///     Cleanup when an instance has been removed.
        /// </summary>
        public void Dispose()
        {
            if (Coop.IsServer && TryGetInstance(out MobileParty party))
            {
                CoopServer.Instance.Persistence.MobilePartyEntityManager.RemoveParty(party);
            }
        }

        /// <summary>
        ///     RPC that is called when the server has created a new <see cref="MobileParty"/>.
        /// </summary>
        private static Invokable OnNewMobilePartyRPC;
        private static void OnNewMobileParty(MobileParty party, Guid guid)
        {
            if(!CoopObjectManager.Objects.ContainsKey(guid))
            {
                Logger.Error($"{party} missing from CoopObjectManager");
                CoopObjectManager.RegisterExistingObject(guid, party);
            }
            else
            {
                MobileParty existingParty = CoopObjectManager.GetObject<MobileParty>(guid);
                if(existingParty != party)
                {
                    Logger.Error($"Inconsistent state in CoopObjectManager");
                    CoopObjectManager.Assert(guid, party);
                }
            }

            if(Coop.IsServer)
            {
                // Now the party exists on all clients, setup the railgun sync for it
                CoopServer.Instance.Persistence.MobilePartyEntityManager.AddParty(party);
            }
            else
            {
                spawnParty(party);
            }
        }
        #endregion

        #region Utils
        /// <summary>
        ///     Returns the corresponding <see cref="MobilePartyManaged"/> instance if it exists,
        ///     otherwise it is created.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="doSyncCreation">
        ///     If a party has to be created:
        ///     - True to send the party to all clients before adding as a managed railgun entity.
        ///     - False will add the party to railgun immediately. Warning: Using False in a game with 
        ///       connected remotes will desync! This is only intended to be used directly after loading.
        /// </param>
        /// <returns></returns>
        public static MobilePartyManaged MakeManaged(MobileParty party, bool doSyncCreation = true)
        {
            if (Instances.TryGetValue(party, out MobilePartyManaged instance))
            {
                return instance;
            }
            return new MobilePartyManaged(party, doSyncCreation);
        }

        private static void spawnParty(MobileParty party)
        {
            party.IsInspected = false;

            // Detele locatorNodeIndex cache from serializer to add it back to the client.
            typeof(MobileParty).GetField("_locatorNodeIndex", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(party, -1);

            // Initializes visuals & infos, needed otherwise party is not visible.
            typeof(PartyBase).GetMethod("OnFinishLoadState", BindingFlags.Instance | BindingFlags.NonPublic)?
                .Invoke(party.Party, new object[] { });

            MethodInfo _AddMobileParty = typeof(CampaignObjectManager).GetMethod("AddMobileParty", BindingFlags.NonPublic | BindingFlags.Instance);
            _AddMobileParty.Invoke(Campaign.Current.CampaignObjectManager, new object[] { party });
            CampaignEventDispatcher.Instance.OnMobilePartyCreated(party);
        }
        #endregion

        #region Game state manipulation
        /// <summary>
        ///     Generated patches to work with movement & position data of a <see cref="MobileParty"/>.
        /// </summary>
        internal static MapMovementPatches MovementPatches { get; }

        /// <summary>
        ///     <see cref="ISynchronization"/> implementation specifically for party movement.
        /// </summary>
        public static MobilePartyMovementSync MovementSync { get; }

        /// <summary>
        ///     Applies the authoritative state of a party to the local game state. This should be called
        ///     at the end of each frame.
        /// </summary>
        public static void ApplyAuthoritativeState(MobileParty party)
        {
            if(Instances.TryGetValue(party, out MobilePartyManaged instance))
            {
                instance.m_Movement.ApplyAuthoritativeState(party);
            }
        }
        
        /// <summary>
        ///     Issue an authoritative (serverside) change of the position of a mobileparty.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="posCurrent"></param>
        /// <param name="facingDirection"></param>
        public static void AuthoritativePositionChange(MobileParty party, Vec2 posCurrent, Vec2? facingDirection)
        {
            if (Instances.TryGetValue(party, out MobilePartyManaged instance))
            {
                instance.m_Movement.SetPosition(party, posCurrent, facingDirection);
            }
        }

        /// <summary>
        ///     Issue an authoritative (serverside) change of the movement of a mobileparty.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="movement"></param>
        public static void AuthoritativeMovementChange(MobileParty party, MovementData movement)
        {
            if (Instances.TryGetValue(party, out MobilePartyManaged instance))
            {
                instance.m_Movement.SetMovement(party, movement);
            }
        }

        private CampaignMapMovement m_Movement = new CampaignMapMovement(MovementPatches);
        #endregion

        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
    }
}