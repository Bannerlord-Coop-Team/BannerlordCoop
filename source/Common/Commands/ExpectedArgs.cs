using System;

namespace Common.Commands;

public interface IExpectedArgs
{
    string Name { get; }

    string Description { get; }

    bool IsRequired { get; }
}

public sealed class ExpectedArgs : IExpectedArgs
{
    public ExpectedArgs(string name, string description, bool isRequired = true)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An argument name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("An argument description is required.", nameof(description));

        Name = name;
        Description = description;
        IsRequired = isRequired;
    }

    public string Name { get; }

    public string Description { get; }

    public bool IsRequired { get; }
}
