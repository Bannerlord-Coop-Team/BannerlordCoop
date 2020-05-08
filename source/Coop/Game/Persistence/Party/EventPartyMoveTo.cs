using RailgunNet.Logic;
using RailgunNet.System.Types;
using TaleWorlds.Library;

namespace Coop.Game.Persistence.Party
{
    public class EventPartyMoveTo : RailEvent
    {
        public Vec2 Pos
        {
            get => new Vec2(PosX, PosY);
            set
            {
                PosX = value.X;
                PosY = value.Y;
            }
        }

        [EventData] public EntityId EntityId { get; set; }
        [EventData] [Compressor(typeof(Compression.Coordinate))] private float PosX { get; set; }
        [EventData] [Compressor(typeof(Compression.Coordinate))] private float PosY { get; set; }

        protected override void Execute(RailRoom room, RailController sender)
        {
            MobilePartyEntityServer entity = Find<MobilePartyEntityServer>(
                EntityId,
                RailPolicy.NoProxy);
            if (entity != null)
            {
                entity.State.PosX = PosX;
                entity.State.PosY = PosY;
            }
        }
    }
}
