using System;
using System.Collections.Generic;

namespace Missions.Naval;

/// <summary>Takes items out of a vanilla list for one call and puts them back where they stood after it.</summary>
internal static class ListTakeOut
{
    /// <summary>Removes the items <paramref name="keep"/> rejects, returning each with its index, lowest first.</summary>
    internal static List<(int Index, T Item)> TakeOut<T>(IList<T> list, Func<T, bool> keep)
    {
        var taken = new List<(int Index, T Item)>();
        for (int index = 0; index < list.Count; index++)
        {
            if (!keep(list[index])) taken.Add((index, list[index]));
        }

        for (int i = taken.Count - 1; i >= 0; i--) list.RemoveAt(taken[i].Index);
        return taken;
    }

    /// <summary>Inserts the taken items back at their indices, lowest first, never past the list's end.</summary>
    internal static void PutBack<T>(IList<T> list, List<(int Index, T Item)> taken)
    {
        if (taken == null) return;

        foreach (var (index, item) in taken) list.Insert(Math.Min(index, list.Count), item);
    }
}
