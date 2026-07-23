using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for IsPregnant
/// </summary>
public record ChangePregnant : ICommand
{
    public bool IsPregnant { get; }
    public string HeroId { get; }

    public ChangePregnant(bool isPregnant, string heroId)
    {
        IsPregnant = isPregnant;
        HeroId = heroId;
    }
}