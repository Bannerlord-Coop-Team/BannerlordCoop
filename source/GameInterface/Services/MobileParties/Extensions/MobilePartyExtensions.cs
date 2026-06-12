using Common;
using Common.Logging;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Extensions;

public static class MobilePartyExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();

    /// <summary>
    /// Check to see if the Party is controlled by this client or server.
    /// </summary>
    /// <param name="party">MobileParty to check that is controlled</param>
    /// <returns>true if is controlled otherwise false.</returns>
    public static bool IsControlledByThisInstance(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return false;
        }

        if (ModInformation.IsServer)
        {
            // Server controls all non-player objects
            return !PlayerManager.TryGetControlledObjectInfo(party, out var _);
        }

        if (!PlayerManager.TryGetControlledObjectInfo(party, out var controlledObjectInfo))
            return false;

        return controlledObjectInfo.IsControlled;
    }

    /// <summary>
    /// Checks to see if the MobileParty is controlled by a player.
    /// </summary>
    /// <param name="party">The mobile party that may be controlled by a player</param>
    /// <returns>return true if the MobileParty is a player otherwise false.</returns>
    public static bool IsPlayerParty(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return false;
        }

        return PlayerManager.TryGetControlledObjectInfo(party, out var _);
    }

    /// <summary>
    /// Empties the party's member and prison rosters — the forfeit applied when a player party is
    /// parked for captivity. The server parks the authoritative party
    /// (<c>PlayerCaptivityServerHandler.Handle_PrisonerTaken</c>) and every client parks its own copy
    /// when it applies the replicated capture
    /// (<c>MapEventPartyHandler.Handle_NetworkTakePrisoner</c>); both must forfeit the same rosters or
    /// the release's re-add lands on diverged state, so the shape of the park lives here, once.
    /// Callers run it inside <see cref="Common.Util.AllowedThread"/> — the clear must never broadcast
    /// roster deltas (each side empties its own copy absolutely).
    /// </summary>
    public static void ForfeitRosters(this MobileParty party)
    {
        party.MemberRoster.Clear();
        party.PrisonRoster.Clear();
    }
}