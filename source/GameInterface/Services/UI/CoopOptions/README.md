# Coop Options

The Coop Options menu is split into tab providers and section view models. Providers own stable tab ids, and sections own stable section ids plus their options DTOs.

The JSON store uses those ids as the saved shape. `CoopOptionsData` should stay generic and only provide tab/section storage helpers. Feature-specific options should live beside the provider or section that owns them.

Current persisted shape:

```json
{
  "KillFeedTab": {
    "KillFeedSection": {
      "killFeedColor": {
        "Red": 12,
        "Green": 34,
        "Blue": 56
      }
    }
  }
}
```

The dedicated server stores its authoritative battle size in the same file:

```json
{
  "ServerOptionsTab": {
    "BattleSizeSection": {
      "battleSize": 1000
    }
  }
}
```

For external reads, use provider helpers instead of hard-coding ids. For example, `KillFeedOptionsTabProvider.TryGetKillFeedColor(options, out var color)` reads from the provider-owned tab and section path.

## UI XML

The runtime UI movie is generated from modular XML fragments.

Run this command from the repository root after editing the Coop Options UI fragments:

```powershell
powershell -ExecutionPolicy Bypass -File source\GameInterface\Services\UI\CoopOptions\Generate-CoopOptionsUIMovie.ps1
```

This writes:

```text
UIMovies\CoopOptionsUIMovie.xml
```

The generator starts from `CoopOptionsUIMovie.template.xml`. Provider fragments are included with comments like:

```xml
<!-- COOP_OPTIONS_PROVIDER:Providers/KillFeedTab/KillFeedTab.xml -->
```

Provider fragments can include section fragments:

```xml
<!-- COOP_OPTIONS_SECTION:Sections/KillFeedSection.xml -->
```

Keep provider XML beside its provider class, and keep section XML beside its section class.

To add a new tab provider:

1. Add the provider class under `Providers\<TabName>` with a stable tab id.
2. Add a provider XML fragment beside it.
3. Include that provider fragment from `CoopOptionsUIMovie.template.xml`.
4. Add section classes and XML fragments under the provider's `Sections` folder.
5. Give each section a stable section id and keep its options DTO beside the section.
6. Include those section fragments from the provider XML.
7. Run the generator command again.

Each template, provider fragment, and section fragment must be valid XML on its own.
