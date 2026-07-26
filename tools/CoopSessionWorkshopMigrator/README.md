# CoopSession workshop JSON migrator

Converts a CoopSession JSON file written by the temporary workshop-snapshot branch
back to the normal workshop JSON schema.

```powershell
./tools/CoopSessionWorkshopMigrator/Restore-WorkshopJson.ps1 `
  "C:\path\to\branch-format.json" `
  "C:\path\to\restored.json"
```

The PowerShell script runs the dependency-free .NET converter. It can also be
invoked directly:

```powershell
dotnet run --project tools/CoopSessionWorkshopMigrator -- "<input>" "<output>"
```

The migrator removes only
`WorkshopPlayerData.WorkshopDataByWorkshopId`. It validates and preserves
`WorkshopPlayerData.PlayerWarehouseRosterPerSettlement` and every unrelated JSON
node. Input and output paths must differ.

`MP.json` is a schema reference only. Its values are not copied into the migrated
save.

The production state represented by the removed snapshots belongs to vanilla's
`WorkshopsCampaignBehavior._workshopData` inside the Bannerlord `.sav`. This tool
does not modify or rebuild `.sav` data.
