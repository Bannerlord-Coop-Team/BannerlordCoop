using System;
using Coop.Mod.Persistence.RemoteAction;
using RemoteAction;
using Sync.Store;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    public class Argument_Test
    {
        const int numberOfTests = 2 ^ 10;
        
        [Fact]
        private void StaticInstanceHashes()
        {
            Assert.Equal(Argument.Null.GetHashCode(), Argument.Null.GetHashCode());
            Assert.Equal(Argument.MBObjectManager.GetHashCode(), Argument.MBObjectManager.GetHashCode());
            Assert.Equal(Argument.CurrentCampaign.GetHashCode(), Argument.CurrentCampaign.GetHashCode());
        }
        
        [Fact]
        private void IntHash()
        {
            const int seed = 42;
            Random random = new Random(seed);
            for (int i = 0; i < numberOfTests; ++i)
            {
                int iNumber = random.Next();
                Assert.Equal(new Argument(iNumber).GetHashCode(), new Argument(iNumber).GetHashCode());
                Assert.NotEqual(new Argument(iNumber).GetHashCode(), new Argument((float)iNumber).GetHashCode());
                for (int j = 0; j < numberOfTests; ++j)
                {
                    int iNumberInner = random.Next();
                    Assert.NotEqual(new Argument(iNumber).GetHashCode(), new Argument(iNumberInner).GetHashCode());
                }
            }
        }
        
        [Fact]
        private void FloatHash()
        {
            const int seed = 43;
            Random random = new Random(seed);
            for (int i = 0; i < numberOfTests; ++i)
            {
                float fNumber = (float)random.NextDouble();
                Assert.Equal(new Argument(fNumber).GetHashCode(), new Argument(fNumber).GetHashCode());
                Assert.NotEqual(new Argument(fNumber).GetHashCode(), new Argument((int)fNumber).GetHashCode());
                for (int j = 0; j < numberOfTests; ++j)
                {
                    float fNumberInner = (float)random.NextDouble();
                    Assert.NotEqual(new Argument(fNumber).GetHashCode(), new Argument(fNumberInner).GetHashCode());
                }
            }
        }
        
        [Fact]
        private void MBGuidHash()
        {
            const int seed = 44;
            Random random = new Random(seed);
            for (int i = 0; i < numberOfTests; ++i)
            {
                MBGUID guid = new MBGUID((uint) random.Next());
                Assert.Equal(new Argument(guid).GetHashCode(), new Argument(guid).GetHashCode());
                for (int j = 0; j < numberOfTests; ++j)
                {
                    MBGUID guidInner = new MBGUID((uint) random.Next());
                    Assert.NotEqual(new Argument(guid).GetHashCode(), new Argument(guidInner).GetHashCode());
                }
            }
        }
        
        [Fact]
        private void StoreIdHash()
        {
            const int seed = 45;
            Random random = new Random(seed);
            for (int i = 0; i < numberOfTests; ++i)
            {
                ObjectId guid = new ObjectId((uint) random.Next());
                Assert.Equal(new Argument(guid).GetHashCode(), new Argument(guid).GetHashCode());
                for (int j = 0; j < numberOfTests; ++j)
                {
                    ObjectId guidInner = new ObjectId((uint) random.Next());
                    Assert.NotEqual(new Argument(guid).GetHashCode(), new Argument(guidInner).GetHashCode());
                }
            }
        }
    }
}
