using Common;
using GameInterface.Services.MobileParties;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals;

/// <summary>
/// Limits which party visuals are built to those near the map camera wherever every party is forced visible,
/// which is the server and debug clients.
/// Without this, too many party visuals can be created at once, which causes frequent system access violations
/// on the server and drops debug clients to a low frame rate.
/// </summary>
public interface IMapVisualRange : IGameAbstraction
{
    // Whether a dirty visual for this party should be rebuilt now.
    bool ShouldRefresh(PartyBase party);
}

public class MapVisualRange : IMapVisualRange
{
    private const float VisualRange = 50f;

    public bool ShouldRefresh(PartyBase party)
    {
        if (ModInformation.IsServer || DebugPartyVisibility.ForceAllVisible)
        {
            return IsNearCamera(party);
        }

        return true;
    }

    private static bool IsNearCamera(PartyBase party)
    {
        var cameraView = MapScreen.Instance?.MapCameraView;
        if (cameraView == null || party == null) return true;

        var cameraPosition = cameraView.CameraFrame.origin.AsVec2;
        return party.Position.ToVec2().DistanceSquared(cameraPosition) <= VisualRange * VisualRange;
    }
}
