using Coop.Game.Persistence.World;
using RailgunNet;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;

namespace Coop.Game.Persistence
{
    public static class Registry
    {
        public static RailRegistry Get(Component eComponent, IEnvironment environment)
        {
            RailRegistry reg = new RailRegistry(eComponent);

            switch (eComponent)
            {
                case Component.Client:
                    reg.AddEntityType<WorldEntityClient, WorldState>(new object[] {environment});
                    break;
                case Component.Server:
                    reg.AddEntityType<WorldEntityServer, WorldState>(new object[] {environment});
                    break;
            }

            reg.SetCommandType<DummyCommand>();
            reg.AddEventType<DummyEvent>();

            return reg;
        }

        public class DummyCommand : RailCommand<DummyCommand>
        {
            protected override void CopyDataFrom(DummyCommand other)
            {
            }

            protected override void DecodeData(RailBitBuffer buffer)
            {
            }

            protected override void EncodeData(RailBitBuffer buffer)
            {
            }

            protected override void ResetData()
            {
            }
        }

        public class DummyEvent : RailEvent
        {
            protected override void Execute(RailRoom room, RailController sender)
            {
            }
        }
    }
}
