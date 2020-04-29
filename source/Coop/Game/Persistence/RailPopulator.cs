using Coop.Game.Persistence.World;
using RailgunNet.Connection.Server;

namespace Coop.Game.Persistence
{
    public static class RailPopulator
    {
        public static void Populate(RailServerRoom room)
        {
            room.AddNewEntity<WorldEntityServer>();
        }
    }
}
