namespace GameInterface.Services.MobileParties;

/// <summary>
/// Debug builds reveal every live party (map figure, nameplate, battle icon) so multiplayer sessions
/// can be watched end to end; release builds keep the native fog of war. Kept as a runtime value so
/// tests can pin either behavior regardless of the build configuration they compile under.
/// </summary>
internal static class DebugPartyVisibility
{
#if DEBUG
    public static bool ForceAllVisible { get; set; } = true;
#else
    public static bool ForceAllVisible { get; set; } = false;
#endif
}
