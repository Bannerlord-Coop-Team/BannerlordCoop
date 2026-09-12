using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Commands;

public interface ICoopCommandArgsFactory
{
    ICoopCommandArgs FromValues(IEnumerable<string> values);

    bool TryFromConsoleTokens(
        IEnumerable<string> tokens,
        out ICoopCommandArgs args,
        out string error);
}

public sealed class CoopCommandArgsFactory : ICoopCommandArgsFactory
{
    public ICoopCommandArgs FromValues(IEnumerable<string> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));

        var copiedValues = new List<string>();
        foreach (string value in values)
        {
            if (value == null)
                throw new ArgumentException("Command arguments cannot contain null values.", nameof(values));

            copiedValues.Add(value);
        }

        return new CoopCommandArgs(copiedValues.AsReadOnly());
    }

    public bool TryFromConsoleTokens(
        IEnumerable<string> tokens,
        out ICoopCommandArgs args,
        out string error)
    {
        args = null;
        error = null;

        if (tokens == null)
        {
            error = "Command arguments cannot be null.";
            return false;
        }

        var parsedValues = new List<string>();
        StringBuilder currentValue = null;
        bool insideQuotes = false;

        foreach (string token in tokens)
        {
            if (token == null)
            {
                error = "Command arguments cannot contain null values.";
                return false;
            }

            if (!insideQuotes && token.Length == 0) continue;

            if (currentValue == null)
            {
                currentValue = new StringBuilder();
            }
            else if (insideQuotes)
            {
                currentValue.Append(' ');
            }

            for (int index = 0; index < token.Length; index++)
            {
                char current = token[index];
                if (current == '\\' && index + 1 < token.Length)
                {
                    char escaped = token[index + 1];
                    if (escaped == '\\' || escaped == '"')
                    {
                        currentValue.Append(escaped);
                        index++;
                        continue;
                    }
                }

                if (current == '"')
                {
                    insideQuotes = !insideQuotes;
                    continue;
                }

                currentValue.Append(current);
            }

            if (!insideQuotes)
            {
                parsedValues.Add(currentValue.ToString());
                currentValue = null;
            }
        }

        if (insideQuotes)
        {
            error = "Command arguments contain an unterminated double quote.";
            return false;
        }

        args = new CoopCommandArgs(parsedValues.AsReadOnly());
        return true;
    }
}
