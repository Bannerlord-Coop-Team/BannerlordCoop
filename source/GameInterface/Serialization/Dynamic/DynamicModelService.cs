using GameInterface.Serialization.Dynamic.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Serialization.Dynamic
{
    internal class DynamicModelService : IDynamicModelService
    {
        public readonly IEnumerable<IDynamicSerializer> DynamicSerializers;
        public DynamicModelService(IDynamicModelGenerator modelGenerator)
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
