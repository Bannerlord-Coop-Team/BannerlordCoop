using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Serialization
{
    public class SerializationServiceTests
    {
        [Fact]
        public void SurrogatesAreCollected()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            SurrogateCollector surrogateCollector = new SurrogateCollector(generator);

            generator.Compile();
        }
    }
}
