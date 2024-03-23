using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for _firstName
/// </summary>
public record ChangeFirstName : ICommand
{
    public string NewName { get; }
    public string HeroId { get; }

    public ChangeFirstName(string newName, string heroId)
    {
        NewName = newName;
        HeroId = heroId;
    }
}