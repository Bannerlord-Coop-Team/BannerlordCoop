using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.Extensions;
public static class EnumerableExtensions
{
    public static IEnumerable<Tuple<T1, T2>> Zip<T1, T2>(this IEnumerable<T1> first, IEnumerable<T2> second)
    {
        int firstCount = first.Count();
        int secondCount = second.Count();

        if (firstCount != secondCount)
        {
            throw new ArgumentException($"Enumerable lengths do not match ({firstCount} != {secondCount})");
        }


        var firstEnumerator = first.GetEnumerator();
        var secondEnumerator = second.GetEnumerator();
        firstEnumerator.MoveNext();
        secondEnumerator.MoveNext();
        for (int i = 0; i < firstCount; i++)
        {
            yield return new Tuple<T1, T2>(firstEnumerator.Current, secondEnumerator.Current);
            firstEnumerator.MoveNext();
            secondEnumerator.MoveNext();
        }
    }
}
