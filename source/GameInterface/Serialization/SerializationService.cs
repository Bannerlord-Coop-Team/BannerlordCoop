using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;

namespace GameInterface.Serialization
{
    public interface ISerializationService
    {
    }

    public class SerializationService : ISerializationService
    {
        public IDynamicSerializerCollector DynamicCollector { get; }
        public ISurrogateCollector SurrogateCollector { get; }
        public SerializationService()
        {
            IDynamicModelGenerator modelGenerator = new DynamicModelGenerator();
            DynamicCollector = new DynamicSerializerCollector(modelGenerator);
            SurrogateCollector = new SurrogateCollector(modelGenerator);

            modelGenerator.Compile();
        }
    }
}
