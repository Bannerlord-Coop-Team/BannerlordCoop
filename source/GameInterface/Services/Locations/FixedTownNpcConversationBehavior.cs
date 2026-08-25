using Common.Logging;
using GameInterface.Services.Modules;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Locations;

public sealed class FixedTownNpcConversationBehavior : CampaignBehaviorBase
{
    private const string RootToken = "start";
    private const string CloseToken = "close_window";
    private const int DialoguePriority = 10000;

    private static readonly ILogger Logger = LogManager.GetLogger<FixedTownNpcConversationBehavior>();
    private readonly ICoopModulePathResolver modulePathResolver;

    public FixedTownNpcConversationBehavior(ICoopModulePathResolver modulePathResolver)
    {
        if (modulePathResolver == null) throw new ArgumentNullException(nameof(modulePathResolver));

        this.modulePathResolver = modulePathResolver;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddDialogs);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void AddDialogs(CampaignGameStarter starter)
    {
        string path = modulePathResolver.GetXmlPath(FixedTownNpcService.XmlName);
        foreach (var definition in FixedTownNpcService.ReadDefinitions(path, Logger))
        {
            if (string.IsNullOrWhiteSpace(definition.Dialogue)) continue;

            string dialogueToken = $"coop_fixed_town_npc_{definition.CharacterId}";
            starter.AddDialogLine(
                dialogueToken + "_greeting",
                RootToken,
                dialogueToken,
                "{=!}" + definition.Dialogue,
                () => IsConversationCharacter(
                    definition.CharacterId,
                    CharacterObject.OneToOneConversationCharacter),
                null,
                DialoguePriority,
                null);

            starter.AddPlayerLine(
                dialogueToken + "_farewell",
                dialogueToken,
                CloseToken,
                "{=!}Farewell.",
                null,
                null,
                DialoguePriority,
                null,
                null);
        }
    }

    internal static bool IsConversationCharacter(
        string configuredCharacterId,
        CharacterObject conversationCharacter)
    {
        return string.Equals(
            configuredCharacterId,
            conversationCharacter?.StringId,
            StringComparison.Ordinal);
    }
}
