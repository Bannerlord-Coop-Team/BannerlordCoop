using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for Culture of Hero
/// </summary>
public record ChangeCulture : ICommand
{
    public string CultureStringId { get; }
    public string HeroId { get; }

    public ChangeCulture(string cultureStringId, string heroId)
    {
        CultureStringId = cultureStringId;
        HeroId = heroId;
    }
}