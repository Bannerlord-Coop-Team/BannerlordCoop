using System;
using System.Collections.Generic;
using Coop.Mod.Persistence.RPC;
using Sync;
using Sync.Store;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    public class MethodCall_Hash_Test
    {
        private Argument GetRandomArgument(Random random)
        {
            int range = Enum.GetNames(typeof(EventArgType)).Length;
            EventArgType eType = (EventArgType) random.Next(0, range);
            switch (eType)
            {
                case EventArgType.Null:
                    return Argument.Null;
                case EventArgType.MBObjectManager:
                    return Argument.MBObjectManager;
                case EventArgType.MBObject:
                    return new Argument(new MBGUID((uint) random.Next()));
                case EventArgType.Int:
                    return new Argument(random.Next());
                case EventArgType.Float:
                    return new Argument((float) random.NextDouble());
                case EventArgType.StoreObjectId:
                    return new Argument(new ObjectId((uint) random.Next()));
                case EventArgType.CurrentCampaign:
                    return Argument.CurrentCampaign;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private List<Argument> GetRandomArguments(Random random, int iNumberOfArguments)
        {
            List<Argument> args = new List<Argument>();
            for (int i = 0; i < iNumberOfArguments; ++i)
            {
                args.Add(GetRandomArgument(random));
            }

            return args;
        }

        private MethodCall GetRandomMethodCall(Random random)
        {
            int iNumberOfArguments = random.Next(0, 10);
            return new MethodCall(
                new MethodId(random.Next()),
                GetRandomArgument(random),
                GetRandomArguments(random, iNumberOfArguments));
        }
        
        const int numberOfTests = 2 ^ 10;
        
        [Fact]
        private void Hash()
        {
            const int seed = 42;
            Random random = new Random(seed);

            for (int i = 0; i < numberOfTests; ++i)
            {
                MethodCall call0 = GetRandomMethodCall(random);
                Assert.Equal(call0.GetHashCode(), call0.GetHashCode());
                for (int j = 0; j < numberOfTests; ++j)
                {
                    MethodCall call1 = GetRandomMethodCall(random);
                    Assert.Equal(call1.GetHashCode(), call1.GetHashCode());
                    Assert.NotEqual(call0.GetHashCode(), call1.GetHashCode());
                }
            }
        }
    }
}
