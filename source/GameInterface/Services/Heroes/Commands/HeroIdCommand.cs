using Common.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using System;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Commands;

public interface IHeroIdCommand : ICoopCommand
{
}

public sealed class HeroIdCommand : IHeroIdCommand
{
    private readonly IObjectManager objectManager;

    public HeroIdCommand(IObjectManager objectManager)
    {
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));

        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.hero";

    public string Name => "id";

    public string Description => "Finds registered ids for heroes with an exact display name.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs(
            "heroName",
            "The exact display name of the hero to find. Quote multi-word values."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        Campaign campaign = Campaign.Current;
        if (campaign == null)
            return new CoopCommandResult(false, "Campaign is not loaded.", "campaign_unavailable");

        string heroName = args[0];
        var heroes = campaign.CampaignObjectManager.GetAllHeroes()
            .Where(hero => string.Equals(
                hero.Name?.ToString(),
                heroName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (heroes.Count == 0)
            return new CoopCommandResult(false, $"No hero named '{heroName}' was found.", "hero_not_found");

        var output = new StringBuilder();
        foreach (Hero hero in heroes)
        {
            if (objectManager.TryGetId(hero, out string id))
            {
                output.AppendLine($"ID: '{id}', Name: '{hero.Name}', Game StringId: {hero.StringId}");
            }
            else
            {
                output.AppendLine($"Name: '{hero.Name}' was not registered with object manager");
            }
        }

        return new CoopCommandResult(true, output.ToString());
    }
}
