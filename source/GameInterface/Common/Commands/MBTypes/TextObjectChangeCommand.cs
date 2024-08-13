namespace GameInterface.Common.Commands.MBTypes;

public class TextObjectChangeCommand<TClass> : GenericChangeCommand<TClass, string>
{
    public TextObjectChangeCommand(string id, string value, string target) : base(id, value, target)
    {
    }
}