using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for _power
/// </summary>
public record ChangePower : ICommand
{
    public float Power { get; }
    public string HeroId { get; }

    public ChangePower(float power, string heroId)
    {
        Power = power;
        HeroId = heroId;
    }
}