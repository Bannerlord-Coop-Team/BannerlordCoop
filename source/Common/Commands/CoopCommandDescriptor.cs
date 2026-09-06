using System;
using System.Text;

namespace Common.Commands;

public sealed class CoopCommandDescriptor
{
    private readonly IExpectedArgs[] expectedArgs;

    internal CoopCommandDescriptor(
        string prefix,
        string name,
        string description,
        IExpectedArgs[] expectedArgs)
    {
        if (expectedArgs == null) throw new ArgumentNullException(nameof(expectedArgs));

        Prefix = prefix;
        Name = name;
        FullName = $"{prefix}.{name}";
        Description = description;
        this.expectedArgs = (IExpectedArgs[])expectedArgs.Clone();
        Usage = BuildUsage();
    }

    public string Prefix { get; }

    public string Name { get; }

    public string FullName { get; }

    public string Usage { get; }

    public string Description { get; }

    public IExpectedArgs[] ExpectedArgs => (IExpectedArgs[])expectedArgs.Clone();

    private string BuildUsage()
    {
        var usage = new StringBuilder("Usage: ");
        usage.Append(FullName);
        foreach (IExpectedArgs expectedArg in expectedArgs)
        {
            usage.Append(expectedArg.IsRequired ? " <" : " [<");
            usage.Append(expectedArg.Name);
            usage.Append(expectedArg.IsRequired ? ">" : ">]");
        }

        if (expectedArgs.Length == 0) return usage.ToString();

        usage.AppendLine();
        usage.AppendLine();
        usage.AppendLine("Parameters:");
        foreach (IExpectedArgs expectedArg in expectedArgs)
        {
            usage.Append("- ");
            usage.Append(expectedArg.Name);
            usage.Append(expectedArg.IsRequired ? " (required): " : " (optional): ");
            usage.AppendLine(expectedArg.Description);
        }

        usage.AppendLine();
        usage.Append("Note: Wrap parameter values containing spaces in double quotes.");
        return usage.ToString();
    }
}
