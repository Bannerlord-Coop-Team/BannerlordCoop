using ProtoBuf;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public struct AgentData
    {
        // mountId: the mount's registry id (resolved by the caller — this ctor has no registry access), carried
        // so the receiver can attach the puppet to the exact horse; Guid.Empty when unregistered/unmounted.
        public AgentData(Agent agent, System.Guid mountId = default)
        {
            Position = agent.Position;
            MovementDirection = agent.GetMovementDirection();
            LookDirection = agent.LookDirection;
            InputVector = agent.MovementInputVector;
            Speed = agent.GetRealGlobalVelocity().AsVec2.Length;
            GuardState = ToWireGuardState(agent.CurrentGuardMode);

            AgentEquipment = new AgentEquipmentData(agent);

            // The rider can be active while its mount is mid-teardown (e.g. right after a battle concludes):
            // reading the mount's native state (MovementInputVector, etc.) then access-violates. Only capture
            // the mount while it is itself active — mirrors the rider guard in AgentMovementHandler.PollMovement
            // and the horse.IsActive() check in SyncMountState.
            Agent mount = agent.MountAgent;
            if (mount != null && mount.IsActive())
            {
                MountData = new AgentMountData(mount, mountId);
            }
            else
            {
                MountData = null;
            }
        }

        public void Apply(Agent agent)
        {
            // if the player is dead, dont sync anything
            if (agent.Health <= 0)
            {
                return;
            }

            // NOTE: position is NOT applied here. It is reconciled per-frame by AgentPositionInterpolator (fed
            // this packet's Position by AgentMovementHandler), so the ease is decoupled from the packet cadence.
            // Everything below is per-packet state that drives the puppet's own walk + animation.

            agent.SetMovementDirection(MovementDirection);

            // apply the agent's look direction
            agent.LookDirection = LookDirection;

            // The raw owner input is local-frame and unrepresentative for AI movement modes (native retreat
            // drives the owner with no input), so an on-foot puppet fed it walks while its position target
            // sprints — the walk-lag-snap desync. Human locomotion is procedural from the input, so derive
            // the throttle from the owner's real ground speed; keep the owner's strafe direction when it has
            // one. Mounted riders keep the raw input — the mount's pace rides its synced channel-0 gait.
            if (agent.HasMount)
            {
                agent.MovementInputVector = InputVector;
            }
            else
            {
                float maxSpeed = agent.GetMaximumForwardUnlimitedSpeed();
                float throttle = maxSpeed > 0f ? MBMath.ClampFloat(Speed / maxSpeed, 0f, 1f) : 0f;
                agent.MovementInputVector = InputVector.LengthSquared > 0.0001f
                    ? InputVector.Normalized() * throttle
                    : new Vec2(0f, throttle);
            }

            // Update equipment
            AgentEquipment.Apply(agent);

            // A held block is continuous state like the inputs above, so it rides every snapshot (self-healing
            // over the unreliable channel). Applied after equipment: guarding needs the weapon/shield wielded.
            ApplyGuardState(agent, FromWireGuardState(GuardState));

            // NOTE: actions/animations are NOT applied here anymore. They are events, not continuous state, so
            // they are synced separately and on-change by AgentActionHandler (reliable-ordered), not polled with
            // movement. This keeps the movement packet purely continuous state.

            // Update mount
            if (agent.HasMount)
            {
                MountData?.ApplyMount(agent.MountAgent);
            }
        }

        /// <summary>
        /// [Game thread] Assert the owner's held guard (block) on the puppet. Blocking is a native STATE the
        /// engine maintains, not just an animation: a Controller.None puppet has nothing writing its guard
        /// (movement flags are input, consumed only for player/AI-controlled agents — persisting them on a
        /// puppet does nothing), so without this the defend action the action sync starts is blended right
        /// back out. SetWeaponGuard is the engine's scripted-agent guard API (SandBox agent behaviors hold
        /// NPC guards with it); asserting it also makes the block REAL for local melee collision, so a swing
        /// at the puppet resolves as blocked on this client the same way it does on the owner's. Only touched
        /// on CHANGE — this runs per snapshot (~100 Hz) and the guard rarely moves. Public and static so the
        /// guard rule is testable headless.
        /// </summary>
        public static void ApplyGuardState(Agent agent, Agent.GuardMode guardMode)
        {
            if (agent.CurrentGuardMode == guardMode) return;

            if (guardMode < Agent.GuardMode.Up)
            {
                agent.ResetGuard();
                return;
            }

            Agent.UsageDirection direction = GuardModeToDefendDirection(guardMode);
            if (direction == Agent.UsageDirection.None) return; // unknown wire value — leave the guard alone

            agent.SetWeaponGuard(direction);
        }

        private static Agent.UsageDirection GuardModeToDefendDirection(Agent.GuardMode mode)
        {
            switch (mode)
            {
                case Agent.GuardMode.Up: return Agent.UsageDirection.DefendUp;
                case Agent.GuardMode.Down: return Agent.UsageDirection.DefendDown;
                case Agent.GuardMode.Left: return Agent.UsageDirection.DefendLeft;
                case Agent.GuardMode.Right: return Agent.UsageDirection.DefendRight;
                default: return Agent.UsageDirection.None;
            }
        }

        // Wire encoding shifts GuardMode by +1 so "no guard" (None = -1) is protobuf's implicit default 0:
        // the common not-blocking case costs no bytes, and an absent field decodes as None (a raw -1 would
        // also be a 10-byte varint on every snapshot of every agent).
        private static int ToWireGuardState(Agent.GuardMode mode) =>
            mode >= Agent.GuardMode.Up ? (int)mode + 1 : 0;

        private static Agent.GuardMode FromWireGuardState(int state) =>
            state > 0 ? (Agent.GuardMode)(state - 1) : Agent.GuardMode.None;

        [ProtoMember(1)]
        public Vec3 Position { get; }
        [ProtoMember(2)]
        public Vec2 InputVector { get; }
        [ProtoMember(3)]
        public Vec3 LookDirection { get; }
        [ProtoMember(4)]
        public Vec2 MovementDirection { get; }
        [ProtoMember(5)]
        public AgentEquipmentData AgentEquipment { get; }
        // 6 was ActionData — actions moved to the event-driven AgentActionHandler; tag left unused for wire stability.
        [ProtoMember(7)]
        public AgentMountData MountData { get; }
        /// <summary>The owner's real ground speed, m/s — drives the on-foot puppet's locomotion throttle.</summary>
        [ProtoMember(8)]
        public float Speed { get; }
        /// <summary>The owner's held guard (block) state: <see cref="Agent.GuardMode"/> shifted by +1 on the
        /// wire (0 = no guard) — see <c>ToWireGuardState</c>.</summary>
        [ProtoMember(9)]
        public int GuardState { get; }
    }
}
