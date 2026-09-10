using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Common.Network;

/// <summary>Compares reference types by object identity.</summary>
public sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

    private ReferenceComparer()
    {
    }

    public bool Equals(T x, T y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
