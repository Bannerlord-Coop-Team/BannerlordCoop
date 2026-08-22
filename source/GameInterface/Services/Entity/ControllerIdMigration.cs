using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;

namespace GameInterface.Services.Entity;

public interface IControllerIdMigration
{
    bool TryMigrate(
        string legacyControllerId,
        string controllerId,
        out Player migratedPlayer);
}

public class ControllerIdMigration : IControllerIdMigration
{
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnershipRegistry issueOwnershipRegistry;
    private readonly IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry;

    public ControllerIdMigration(
        IPlayerManager playerManager,
        IIssueOwnershipRegistry issueOwnershipRegistry,
        IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry)
    {
        this.playerManager = playerManager;
        this.issueOwnershipRegistry = issueOwnershipRegistry;
        this.troopsRegistry = troopsRegistry;
    }

    public bool TryMigrate(
        string legacyControllerId,
        string controllerId,
        out Player migratedPlayer)
    {
        if (!playerManager.TryMigrateControllerId(
            legacyControllerId,
            controllerId,
            out migratedPlayer))
            return false;

        issueOwnershipRegistry.MigrateControllerId(legacyControllerId, controllerId);
        troopsRegistry.MigrateControllerId(legacyControllerId, controllerId);
        return true;
    }
}
