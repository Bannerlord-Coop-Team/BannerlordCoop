using Coop.Mod.Config;
using HarmonyLib;
using System;

namespace Coop.Mod
{
    public class ManagedTypesInitializer
    {
        /// <summary>
        /// Initialized types that will be managed by the <see cref="CoopObjectManager"/>.
        /// </summary>
        public static void InitializeTypes(Harmony harmony)
        {
            Type[] managedTypes = ManagedTypesConfig.ManagedTypes;
            foreach (Type type in managedTypes)
            {
                typeof(CoopObjectManager).GetMethod("PatchType").MakeGenericMethod(type).Invoke(null, new object[] { harmony });
            }
        }
    }
}
