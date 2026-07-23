using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for _birthDay
/// </summary>
public record ChangeBirthDay : ICommand
{
    public long BirthDay { get; }
    public string HeroId { get; }

    public ChangeBirthDay(long birthDay, string heroId)
    {
        BirthDay = birthDay;
        HeroId = heroId;
    }
}