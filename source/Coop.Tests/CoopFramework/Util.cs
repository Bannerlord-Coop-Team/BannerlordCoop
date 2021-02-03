using System;
using System.Linq;
using System.Reflection;
using CoopFramework;

namespace Coop.Tests.CoopFramework
{
    public class Util
    {
        public static void CallPatchInitializer(Type t)
        {
            var methods= t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var initializer in methods.Where(m => m.IsDefined(typeof(PatchInitializerAttribute))))
            {
                initializer.Invoke(null, null);
            }
        }
    }
}