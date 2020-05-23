using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sync.Reflection
{
    public static class Extensions
    {
        public static MethodInfo[] GetDeclaredMethods(this Type type)
        {
            return type.GetMethods(
                BindingFlags.DeclaredOnly |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Public);
        }

        public static TValue Assert<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            where TValue : new()
        {
            if (dict.TryGetValue(key, out TValue val))
            {
                return val;
            }

            val = new TValue();
            dict.Add(key, val);
            return val;
        }
    }
}
