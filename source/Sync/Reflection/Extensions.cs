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

        /// <summary>
        ///     Try to get the current value in the dictionary, if not exists, it creates a new <typeparamref name="TValue" /> and
        ///     it's added to the dictionary with key <typeparamref name="TKey" />
        /// </summary>
        /// <returns>Current value or the added object of type <typeparamref name="TValue" /></returns>
        public static TValue Assert<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            where TValue : new()
        {
            if (dict.TryGetValue(key, out var val)) return val;

            val = new TValue();
            dict.Add(key, val);
            return val;
        }
    }
}