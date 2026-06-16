using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Identifies the re-enabled ambient crowd (townsfolk, villagers, merchants, workshop workers) so it can be
/// made static and non-interactable, without affecting other non-hero NPCs spawned during the same scene pass
/// (e.g. the mercenary recruit or board-game host, whose behaviors co-op does not re-enable through here).
/// </summary>
/// <remarks>
/// The four re-enabled behaviors' spawn handlers run inside <see cref="BeginScope"/>/<see cref="EndScope"/>, so
/// any <see cref="LocationCharacter"/> constructed during that window is ambient crowd; its (shared culture)
/// character template is recorded so every agent later built from it is recognised. Main-thread only.
/// </remarks>
internal static class AmbientCrowd
{
    [ThreadStatic]
    private static int scopeDepth;

    private static readonly HashSet<CharacterObject> ambientCharacters = new HashSet<CharacterObject>();

    /// <summary>True while a re-enabled ambient behavior's spawn handler is running.</summary>
    public static bool IsSpawning => scopeDepth > 0;

    public static void BeginScope() => scopeDepth++;

    public static void EndScope() => scopeDepth--;

    public static void Mark(CharacterObject character)
    {
        if (character != null) ambientCharacters.Add(character);
    }

    public static bool IsAmbient(CharacterObject character) =>
        character != null && ambientCharacters.Contains(character);
}
