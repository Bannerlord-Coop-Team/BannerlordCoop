using GameInterface.Services.Modules;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;

namespace GameInterface.Services.Locations;

/// <summary>
/// Adds the non-hero characters declared in coop_fixed_town_npcs.xml to their configured
/// settlement locations. Bannerlord reads the character data from the same XML while this
/// service reads only the coop_* placement attributes, keeping each NPC in one editable record.
/// </summary>
internal sealed class FixedTownNpcService
{
    internal const string XmlName = "coop_fixed_town_npcs";

    private const string DefaultLocationId = "tavern";
    private const string DefaultSpawnTag = "npc_common";

    private readonly ILogger logger;
    private readonly IObjectManager objectManager;
    private readonly Lazy<IReadOnlyList<FixedTownNpcDefinition>> definitions;

    public FixedTownNpcService(
        ILogger logger,
        IObjectManager objectManager,
        ICoopModulePathResolver modulePathResolver)
        : this(logger, objectManager, GetPathProvider(modulePathResolver))
    {
    }

    internal FixedTownNpcService(ILogger logger, IObjectManager objectManager, Func<string> pathProvider)
    {
        this.logger = logger;
        this.objectManager = objectManager;
        definitions = new Lazy<IReadOnlyList<FixedTownNpcDefinition>>(
            () => ReadDefinitions(pathProvider(), logger));
    }

    private static Func<string> GetPathProvider(ICoopModulePathResolver modulePathResolver)
    {
        if (modulePathResolver == null) throw new ArgumentNullException(nameof(modulePathResolver));

        return () => modulePathResolver.GetXmlPath(XmlName);
    }

    public void Populate(Settlement settlement)
    {
        if (settlement?.LocationComplex == null) return;

        foreach (var definition in definitions.Value)
        {
            if (string.Equals(definition.SettlementId, settlement.StringId, StringComparison.Ordinal) == false)
                continue;

            var location = settlement.LocationComplex.GetLocationWithId(definition.LocationId);
            if (location == null)
            {
                logger.Warning(
                    "Fixed-town NPC {CharacterId} targets missing location {LocationId} in {SettlementId}",
                    definition.CharacterId,
                    definition.LocationId,
                    definition.SettlementId);
                continue;
            }

            if (objectManager.TryGetObject(definition.CharacterId, out CharacterObject character) == false)
            {
                logger.Warning(
                    "Fixed-town NPC character {CharacterId} is not registered; check {XmlName}.xml",
                    definition.CharacterId,
                    XmlName);
                continue;
            }

            if (character.IsHero || character.IsTemplate)
            {
                logger.Warning(
                    "Fixed-town NPC {CharacterId} must be a non-hero, non-template NPCCharacter",
                    definition.CharacterId);
                continue;
            }

            if (settlement.LocationComplex.GetFirstLocationCharacterOfCharacter(character) != null)
                continue;

            try
            {
                var locationCharacter = LocationCharacterFactory.Create(
                    character,
                    originParty: null,
                    specialItem: null,
                    spawnTag: definition.SpawnTag,
                    actionSetCode: null,
                    behaviorsMethodName: null,
                    characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
                    fixedLocation: true,
                    useCivilianEquipment: true);

                location.AddCharacter(locationCharacter);
            }
            catch (Exception e)
            {
                logger.Warning(
                    e,
                    "Failed to place fixed-town NPC {CharacterId} in {SettlementId}/{LocationId}",
                    definition.CharacterId,
                    definition.SettlementId,
                    definition.LocationId);
            }
        }
    }

    internal static IReadOnlyList<FixedTownNpcDefinition> ReadDefinitions(string path, ILogger logger)
    {
        var result = new List<FixedTownNpcDefinition>();

        if (string.IsNullOrWhiteSpace(path) || File.Exists(path) == false)
        {
            logger.Warning("Fixed-town NPC data file not found at {Path}", path);
            return result;
        }

        try
        {
            var document = new XmlDocument();
            document.Load(path);

            var characterNodes = document.SelectNodes("/NPCCharacters/NPCCharacter");
            if (characterNodes == null) return result;

            var seenCharacterIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlNode node in characterNodes)
            {
                string settlementId = GetAttribute(node, "coop_settlement");
                if (string.IsNullOrWhiteSpace(settlementId)) continue;

                string characterId = GetAttribute(node, "id");
                if (string.IsNullOrWhiteSpace(characterId))
                {
                    logger.Warning("Fixed-town NPC entry for {SettlementId} has no character id", settlementId);
                    continue;
                }

                string enabledText = GetAttribute(node, "coop_enabled");
                if (string.IsNullOrWhiteSpace(enabledText) == false)
                {
                    if (bool.TryParse(enabledText, out bool enabled) == false)
                    {
                        logger.Warning(
                            "Fixed-town NPC {CharacterId} has invalid coop_enabled value {Value}",
                            characterId,
                            enabledText);
                        continue;
                    }

                    if (enabled == false) continue;
                }

                if (seenCharacterIds.Add(characterId) == false)
                {
                    logger.Warning("Fixed-town NPC {CharacterId} is configured more than once", characterId);
                    continue;
                }

                string locationId = GetAttribute(node, "coop_location");
                string spawnTag = GetAttribute(node, "coop_spawn_tag");
                string dialogue = GetAttribute(node, "coop_dialogue");

                result.Add(new FixedTownNpcDefinition(
                    characterId,
                    settlementId,
                    string.IsNullOrWhiteSpace(locationId) ? DefaultLocationId : locationId,
                    string.IsNullOrWhiteSpace(spawnTag) ? DefaultSpawnTag : spawnTag,
                    dialogue));
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "Unable to read fixed-town NPC data from {Path}", path);
        }

        return result;
    }

    private static string GetAttribute(XmlNode node, string name)
    {
        return node.Attributes?[name]?.Value?.Trim();
    }
}

internal sealed class FixedTownNpcDefinition
{
    public string CharacterId { get; }
    public string SettlementId { get; }
    public string LocationId { get; }
    public string SpawnTag { get; }
    public string Dialogue { get; }

    public FixedTownNpcDefinition(
        string characterId,
        string settlementId,
        string locationId,
        string spawnTag,
        string dialogue)
    {
        CharacterId = characterId;
        SettlementId = settlementId;
        LocationId = locationId;
        SpawnTag = spawnTag;
        Dialogue = dialogue;
    }
}
