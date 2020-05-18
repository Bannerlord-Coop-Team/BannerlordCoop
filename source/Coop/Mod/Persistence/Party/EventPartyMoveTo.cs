using System.Linq;
using NLog;
using RailgunNet.Logic;
using RailgunNet.System.Types;

namespace Coop.Mod.Persistence.Party
{
    public class EventPartyMoveTo : RailEvent
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [EventData] public EntityId EntityId { get; set; }

        [EventData] public MovementState Movement { get; set; }

        protected override void Execute(RailRoom room, RailController sender)
        {
            if (TryFind(EntityId, out MobilePartyEntityServer entity))
            {
                if (sender.ControlledEntities.Contains(entity))
                {
                    Logger.Trace(
                        "[T {}] Ack move entity {id} to {position}.",
                        room.Tick,
                        EntityId,
                        Movement);
                    entity.State.Movement = Movement;
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
}
