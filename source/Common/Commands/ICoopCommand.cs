namespace Common.Commands;

public interface ICoopCommand
{
    string Prefix { get; }

    string Name { get; }

    string Description { get; }

    CoopCommandSide Side { get; }

    IExpectedArgs[] ExpectedArgs { get; }

    CoopCommandResult ProcessCommand(ICoopCommandArgs args);
}
