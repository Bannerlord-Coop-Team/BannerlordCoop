using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for _name
/// </summary>
public record ChangeName : ICommand
{
    public string NewName { get; }
    public string HeroId { get; }

    public ChangeName(string newName, string heroId)
    {
        NewName = newName;
        HeroId = heroId;
    }
}