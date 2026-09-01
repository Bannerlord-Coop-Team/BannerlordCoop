using System;
using System.Collections;
using System.Collections.Generic;

namespace Common.Commands;

public interface ICoopCommandArgs : IReadOnlyList<string>
{
}

internal sealed class CoopCommandArgs : ICoopCommandArgs
{
    private readonly IReadOnlyList<string> values;

    public CoopCommandArgs(IReadOnlyList<string> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));

        this.values = values;
    }

    public int Count => values.Count;

    public string this[int index] => values[index];

    public IEnumerator<string> GetEnumerator()
    {
        return values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
