using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    public class PropertyAccess
    {
        public PropertyAccess([NotNull] PropertyInfo property)
        {
            MethodInfo getter = AccessTools.PropertyGetter(property.DeclaringType, property.Name);
            MethodInfo setter = AccessTools.PropertySetter(property.DeclaringType, property.Name);
            if (getter == null || setter == null)
            {
                throw new Exception($"Unable to create property access for {property}.");
            }

            Getter = new MethodAccess(getter);
            Setter = new MethodAccess(setter);
        }

        [NotNull] public MethodAccess Getter { get; }
        [NotNull] public MethodAccess Setter { get; }
    }
}
