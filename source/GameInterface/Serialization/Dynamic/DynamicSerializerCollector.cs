using GameInterface.Serialization.Dynamic.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Serialization.Dynamic
{
    public interface IDynamicSerializerCollector
    {
    }

    public class DynamicSerializerCollector : IDynamicSerializerCollector
    {
        public readonly IEnumerable<IDynamicSerializer> DynamicSerializers;
        public DynamicSerializerCollector(IDynamicModelGenerator modelGenerator)
        {
            Assembly asm = GetType().Assembly;
            string Namespace = GetType().Namespace;

            IEnumerable<Type> types = asm.GetTypes()
                .Where(t => typeof(IDynamicSerializer).IsAssignableFrom(t) &&
                            t.Namespace.StartsWith(Namespace));

            List<IDynamicSerializer> serializers = types
                .Select(t => (IDynamicSerializer)Activator.CreateInstance(t, new object[] { modelGenerator }))
                .ToList();

            DynamicSerializers = serializers;

            modelGenerator.Compile();
        }
    }
}
