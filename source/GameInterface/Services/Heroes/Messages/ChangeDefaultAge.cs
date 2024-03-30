using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for _defaultAge
/// </summary>
public record ChangeDefaultAge : ICommand
{
    public float Age { get; }
    public string HeroId { get; }

    public ChangeDefaultAge(float age, string heroId)
    {
        Age = age;
        HeroId = heroId;
    }
}