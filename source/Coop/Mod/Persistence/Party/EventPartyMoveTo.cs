using System.Linq;
using NLog;
using RailgunNet.Logic;
using RailgunNet.System.Types;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Event raised by clients if they want to move a party. The client may only send this
    ///     event for parties that are under its control.
    /// </summary>
    public class EventPartyMoveTo : RailEvent
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [EventData] public EntityId EntityId { get; set; }

        [EventData] public MovementState Movement { get; set; }

        protected override void Execute(RailRoom room, RailController sender)
        {
            if (!TryFind(EntityId, out MobilePartyEntityServer entity))
            {
                return;
            }

            if (sender.ControlledEntities.Contains(entity))
            {
                Logger.Trace(
                    "[{tick}] Ack move entity {id} to {position}.",
                    room.Tick,
                    EntityId,
                    Movement.ToString());
                entity.ApplyPlayerMove(Movement.ToData());
            }
            else
            {
                Logger.Warn(
                    "{controller} tried to move entity {id} without permission.",
                    sender,
                    EntityId);
            }
        }
    }
}