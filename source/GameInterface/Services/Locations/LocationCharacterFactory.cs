using Common.Logging;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.AI;
using SandBox.Missions.AgentBehaviors;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;

namespace GameInterface.Services.Locations;

/// <summary>
/// Builds <see cref="LocationCharacter"/> instances from synced data and extracts that data from
/// live instances. The AddBehaviors delegate cannot travel over the network, so it is carried as
/// the full name of its static method and rebound here; instance-bound delegates fall back to a
/// default behavior set, which only affects the AI flavor of locally spawned mission agents.
/// </summary>
internal static class LocationCharacterFactory
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationCharacter>();

    private static readonly string CompanionBehaviorsName =
        $"{typeof(BehaviorSets).FullName}.{nameof(BehaviorSets.AddCompanionBehaviors)}";

    /// <summary>
    /// Extracts the synced fields of a roster entry into the internal added event.
    /// </summary>
    public static LocationCharacterAdded CreateAddedEvent(Location location, LocationCharacter locationCharacter)
    {
        var originParty = (locationCharacter.AgentData?.AgentOrigin as PartyAgentOrigin)?.Party?.MobileParty;

        return new LocationCharacterAdded(
            location,
            locationCharacter.Character,
            originParty,
            locationCharacter.SpecialItem,
            locationCharacter.SpecialTargetTag,
            locationCharacter.ActionSetCode,
            ExtractBehaviorsMethodName(locationCharacter),
            (int)locationCharacter.CharacterRelation,
            locationCharacter.FixedLocation,
            locationCharacter.UseCivilianEquipment);
    }

    public static LocationCharacter Create(
        CharacterObject character,
        MobileParty originParty,
        ItemObject specialItem,
        string spawnTag,
        string actionSetCode,
        string behaviorsMethodName,
        int characterRelation,
        bool fixedLocation,
        bool useCivilianEquipment,
        int[] prefabBones = null,
        string[] prefabNames = null)
    {
        IAgentOriginBase origin = originParty != null
            ? new PartyAgentOrigin(originParty.Party, character)
            : (IAgentOriginBase)new SimpleAgentOrigin(character);

        var agentData = new AgentData(origin)
            .Monster(FaceGen.GetMonsterWithSuffix(character.Race, "_settlement"))
            .NoHorses(noHorses: true);

        var locationCharacter = new LocationCharacter(
            agentData,
            ResolveBehaviors(behaviorsMethodName, originParty != null),
            spawnTag,
            fixedLocation,
            (LocationCharacter.CharacterRelations)characterRelation,
            actionSetCode,
            useCivilianEquipment,
            // isFixedCharacter dereferences Settlement.CurrentSettlement, which is null on
            // machines whose player is not inside the settlement.
            isFixedCharacter: false,
            specialItem);

        // Bone-attached carry props (baskets, pitchers, carried goods). Restored so a promoted host's
        // CreateAgentNavigator re-attaches them exactly as a native spawn would.
        if (prefabBones != null && prefabNames != null && prefabBones.Length == prefabNames.Length)
        {
            for (int i = 0; i < prefabBones.Length; i++)
                locationCharacter.PrefabNamesForBones[(sbyte)prefabBones[i]] = prefabNames[i];
        }

        return locationCharacter;
    }

    /// <summary>
    /// Builds the roster entry for a companion of a visiting player party. Vanilla never places
    /// another party's companions, so the server creates these for every visitor itself.
    /// </summary>
    public static LocationCharacter CreateCompanion(Hero companion, MobileParty party, bool useCivilianEquipment)
    {
        return Create(
            companion.CharacterObject,
            party,
            specialItem: null,
            spawnTag: "sp_notable",
            actionSetCode: null,
            behaviorsMethodName: CompanionBehaviorsName,
            characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
            fixedLocation: true,
            useCivilianEquipment: useCivilianEquipment);
    }

    /// <summary>
    /// Extracts a roster entry into its serializable form, resolving object ids. Fails when the
    /// character has no registered id.
    /// </summary>
    public static bool TryCreateData(
        IObjectManager objectManager,
        string locationId,
        LocationCharacter locationCharacter,
        out LocationCharacterData data)
    {
        data = null;

        if (locationCharacter?.Character == null) return false;
        if (objectManager.TryGetId(locationCharacter.Character, out var characterId) == false) return false;

        string originPartyId = null;
        var originParty = (locationCharacter.AgentData?.AgentOrigin as PartyAgentOrigin)?.Party?.MobileParty;
        if (originParty != null)
        {
            objectManager.TryGetId(originParty, out originPartyId);
        }

        string specialItemId = null;
        if (locationCharacter.SpecialItem != null)
        {
            objectManager.TryGetId(locationCharacter.SpecialItem, out specialItemId);
        }

        // Bone-attached carry props: without these on the wire, a puppet plays its carry action set
        // with empty hands (the basket/pitcher is an AgentNavigator prefab, not equipment).
        int[] prefabBones = null;
        string[] prefabNames = null;
        var prefabsForBones = locationCharacter.PrefabNamesForBones;
        if (prefabsForBones != null && prefabsForBones.Count > 0)
        {
            prefabBones = new int[prefabsForBones.Count];
            prefabNames = new string[prefabsForBones.Count];
            int i = 0;
            foreach (var pair in prefabsForBones)
            {
                prefabBones[i] = pair.Key;
                prefabNames[i] = pair.Value;
                i++;
            }
        }

        data = new LocationCharacterData(
            locationId,
            characterId,
            originPartyId,
            specialItemId,
            locationCharacter.SpecialTargetTag,
            locationCharacter.ActionSetCode,
            ExtractBehaviorsMethodName(locationCharacter),
            (int)locationCharacter.CharacterRelation,
            locationCharacter.FixedLocation,
            locationCharacter.UseCivilianEquipment,
            prefabBones,
            prefabNames);

        return true;
    }

    public static string ExtractBehaviorsMethodName(LocationCharacter locationCharacter)
    {
        var method = locationCharacter.AddBehaviors?.Method;
        if (method == null || method.DeclaringType == null) return null;

        if (method.IsStatic)
            return $"{method.DeclaringType.FullName}.{method.Name}";

        // Native ambient entries carry INSTANCE delegates: every townsfolk/villager/tavern creator
        // passes SandBoxManager.Instance.AgentBehaviorManager.Add*Behaviors, whose methods wrap the
        // static BehaviorSets 1:1 (mostly as explicit interface implementations, so method.Name is
        // interface-qualified). Map them to their static counterpart — losing the name here made
        // every reconstructed wanderer fall back to fixed-character behaviors, and a promoted host's
        // adopted crowd stood still.
        if (locationCharacter.AddBehaviors.Target is AgentBehaviorManager)
        {
            var name = method.Name;
            var lastDot = name.LastIndexOf('.');
            if (lastDot >= 0) name = name.Substring(lastDot + 1);

            if (AccessTools.Method(typeof(BehaviorSets), name) == null)
            {
                // One irregular wrapper: AddStealthAgentBehaviors -> BehaviorSets.StealthAgentBehaviors.
                var trimmed = name.StartsWith("Add") ? name.Substring(3) : name;
                if (AccessTools.Method(typeof(BehaviorSets), trimmed) == null)
                {
                    Logger.Warning("Behaviors method {method} has no static BehaviorSets counterpart — the entry will rebuild with default behaviors", name);
                    return null;
                }
                name = trimmed;
            }

            return $"{typeof(BehaviorSets).FullName}.{name}";
        }

        return null;
    }

    private static LocationCharacter.AddBehaviorsDelegate ResolveBehaviors(string behaviorsMethodName, bool isCompanion)
    {
        if (!string.IsNullOrEmpty(behaviorsMethodName))
        {
            try
            {
                var separatorIndex = behaviorsMethodName.LastIndexOf('.');
                var type = AccessTools.TypeByName(behaviorsMethodName.Substring(0, separatorIndex));
                var method = AccessTools.Method(type, behaviorsMethodName.Substring(separatorIndex + 1));

                if (method != null && method.IsStatic)
                {
                    return (LocationCharacter.AddBehaviorsDelegate)Delegate.CreateDelegate(
                        typeof(LocationCharacter.AddBehaviorsDelegate), method);
                }

                Logger.Warning("Could not rebind behaviors method {method}, using default behaviors", behaviorsMethodName);
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Failed to rebind behaviors method {method}, using default behaviors", behaviorsMethodName);
            }
        }

        return isCompanion
            ? BehaviorSets.AddCompanionBehaviors
            : (LocationCharacter.AddBehaviorsDelegate)BehaviorSets.AddFixedCharacterBehaviors;
    }
}
