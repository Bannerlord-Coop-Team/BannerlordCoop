using System.ComponentModel;
using PropertyChanged;
using RailgunNet.Logic;
using TaleWorlds.Library;

namespace Coop.Game.Persistence.Party
{
    public class MobilePartyState : RailState, INotifyPropertyChanged
    {
        public Vec2 Position => new Vec2(PosX, PosY);
        [DoNotNotify] [Immutable] public int PartyId { get; set; }
        [Mutable] [Compressor(typeof(Compression.Coordinate))] public float PosX { get; set; }
        [Mutable] [Compressor(typeof(Compression.Coordinate))] public float PosY { get; set; }
#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
    }
}
