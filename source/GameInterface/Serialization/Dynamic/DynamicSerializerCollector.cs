using GameInterface.Serialization.Dynamic.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Serialization.Dynamic
{
    public interface IDynamicSerializerCollector
    {
        IEnumerable<IDynamicSerializer> DynamicSerializers { get; }
    }

    public class DynamicSerializerCollector : IDynamicSerializerCollector
    {
        public IEnumerable<IDynamicSerializer> DynamicSerializers { get; }
        public DynamicSerializerCollector(IDynamicModelGenerator modelGenerator)
        {
            Assembly asm = GetType().Assembly;
            string Namespace = GetType().Namespace;

            IEnumerable<Type> types = asm.GetTypes()
                .Where(t => typeof(IDynamicSerializer).IsAssignableFrom(t) &&
                            t.Namespace.StartsWith(Namespace) &&
                            t.IsClass);

            ValidateTypes(types);

            List<IDynamicSerializer> serializers = types
                .Select(t => (IDynamicSerializer)Activator.CreateInstance(t, new object[] { modelGenerator }))
                .ToList();

            DynamicSerializers = serializers;
        }

        private void ValidateTypes(IEnumerable<Type> types)
        {
            List<Type> invalidTypes = new List<Type>();
            foreach(var type in types)
            {
                bool hasValidConstructor = type.GetConstructors().Any(
                    c => c.GetParameters().Length == 1 &&
                    c.GetParameters().Single().ParameterType == typeof(IDynamicModelGenerator));

                if (hasValidConstructor == false)
                {
                    invalidTypes.Add(type);
                }
            }

            if(invalidTypes.Count > 0)
            {
                throw new InvalidOperationException($"These types do not have a valid constructor, " +
                    $"expected a constructor with a single parameter of type " +
                    $"{nameof(IDynamicModelGenerator)}. Invalid types {invalidTypes}");
            }
        }
    }
}
