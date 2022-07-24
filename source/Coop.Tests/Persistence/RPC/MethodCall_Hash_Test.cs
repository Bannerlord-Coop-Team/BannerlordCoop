using System;
using System.Collections.Generic;
using Coop.Mod.Persistence.RemoteAction;
using RemoteAction;
using Sync;
using Sync.Call;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
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
                case EventArgType.CoopObjectManagerId:
                    return new Argument(Guid.NewGuid(), true);
                case EventArgType.Guid:
                    return new Argument(Guid.NewGuid(), false);
                case EventArgType.Int:
                    return new Argument(random.Next());
                case EventArgType.Float:
                    return new Argument((float) random.NextDouble());
                case EventArgType.StoreObjectId:
                    return new Argument(new ObjectId((uint) random.Next()));
                case EventArgType.CurrentCampaign:
                    return Argument.CurrentCampaign;
                case EventArgType.Bool:
                    return new Argument(random.Next(2) ==  0);
                case EventArgType.CampaignBehavior:
                    return new Argument(new BannerCampaignBehavior());
                case EventArgType.PartyComponent:
                    return new Argument(new CustomPartyComponent());
                case EventArgType.SmallObjectRaw:
                    int[] intArray = {random.Next(), random.Next()};
                    byte[] raw = new byte[intArray.Length * sizeof(int)];
                    Buffer.BlockCopy(intArray, 0, raw, 0, raw.Length);
                    return new Argument(raw);
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
                new InvokableId(random.Next()),
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
