# Bannerlord Coop - Scaffolding
A dotnet CLI scaffolding tool for the Bannerlord Coop project using razor.

## Installation

### Install via NuGet:
To install the Bannerlord Coop CLI tool, run the following command in your terminal:

```bash
dotnet tool install --global BannerlordCoop.CLI.Scaffolding
```

### Build from source
1. Clone branch, build 'Scaffolderlord' project
2. Pack:
```bash
dotnet pack --configuration Release
```
3. Install from local folder:
```bash
dotnet tool install --global --add-source ./bin/Release BannerlordCoop
```

## Usage

After installing the dotnet tool globally, just call the root command `coop-create` specifying a command with it's argument and options.

The main argument for all commands is the type's fully qualified name and assembly separated by a comma.

Example: `coop-create all "TaleWorlds.CampaignSystem.Siege.BesiegerCamp, TaleWorlds.CampaignSystem" --members SiegeEvent SiegeEngines SiegeStrategy NumberOfTroopsKilledOnSide _leaderParty `

This will generate ALL template files for BesiegerCamp and even prepare autosync and create e2e and commands for all specified members.

Obs: Be aware that nested types use a '+' instead of a dot on their qualified names, for example `SiegeEvent.SiegeEnginesContainer` is `TaleWorlds.CampaignSystem.Siege.SiegeEvent+SiegeEnginesContainer, TaleWorlds.CampaignSystem"`

### Main Commands
- **`all <type-fully-qualified-name> [--members] [--overwrite]`**  
  Generates a registry class for the specified type.

- **`registry <type-fully-qualified-name> [--overwrite]`**  
  Generates a registry class for the specified type.

- **`sync <type-fully-qualified-name> [--members] [--overwrite]`**  
  Generates a sync class for the specified type.

- **`lifetime <type-fully-qualified-name> [--overwrite]`**  
  Generates all lifetime handling files for specified type.

- **`e2e <type-fully-qualified-name> [--members] [--overwrite]`**  
  Generates end-to-end tests.

- **`commands <type-fully-qualified-name> [--members] [--overwrite]`**  
  Generates command-related classes (WIP).

- **`collections <type-fully-qualified-name> [--members] [--overwrite]`**  
  Generates collection-related classes (WIP).

## Options

- **`--overwrite`**: Specifies if existing files should be overwritten, otherwise a new file is created with a suffix (existing files are NOT altered).
- **`--members`**: Specifies which members to include on template generation, for autosync, e2e, commands, etc (currentyl doesn't do collections).
- **`--help`**: Gives details about command.

## All Commands
These are the files the CLI can generate

| Command                                  | File                               | Namespace                                         | Output Path                                                                           |
|------------------------------------------|------------------------------------|--------------------------------------------------|---------------------------------------------------------------------------------------|
| `registry`                               | `TypeName`Registry.cs              | GameInterface.Services.`TypeName`s               | GameInterface\\Services\\`TypeName`s\\`TypeName`Registry.cs                           |
| `sync`                                   | `TypeName`Sync.cs                  | GameInterface.Services.`TypeName`s               | GameInterface\\Services\\`TypeName`s\\`TypeName`Sync.cs                               |
| `lifetime`, `lifetime-handler`           | `TypeName`LifetimeHandler.cs       | GameInterface.Services.`TypeName`s.Handlers      | GameInterface\\Services\\`TypeName`s\\Handlers\\`TypeName`LifetimeHandler.cs          |
| `lifetime`, `lifetime-patches`           | `TypeName`LifetimePatches.cs       | GameInterface.Services.`TypeName`s.Patches       | GameInterface\\Services\\`TypeName`s\\Patches\\`TypeName`LifetimePatches.cs           |
| `lifetime`, `lifetime-messages-created`  | `TypeName`Created.cs               | GameInterface.Services.`TypeName`s.Messages      | GameInterface\\Services\\`TypeName`s\\Messages\\Lifetime\\`TypeName`Created.cs        |
| `lifetime`, `lifetime-messages-destroyed`| `TypeName`Destroyed.cs             | GameInterface.Services.`TypeName`s.Messages      | GameInterface\\Services\\`TypeName`s\\Messages\\Lifetime\\`TypeName`Destroyed.cs      |
| `lifetime`, `lifetime-messages-create`   | NetworkCreate`TypeName`.cs         | GameInterface.Services.`TypeName`s.Messages      | GameInterface\\Services\\`TypeName`s\\Messages\\Lifetime\\NetworkCreate`TypeName`.cs  |
| `lifetime`, `lifetime-messages-destroy`  | NetworkDestroy`TypeName`.cs        | GameInterface.Services.`TypeName`s.Messages      | GameInterface\\Services\\`TypeName`s\\Messages\\Lifetime\\NetworkDestroy`TypeName`.cs |
| `collection`, `collection-handler`       | `TypeName``CollectionName`Handler.cs | GameInterface.Services.`TypeName`s.Handlers    | GameInterface\\Services\\`TypeName`s\\Handlers\\`TypeName``CollectionName`Handler.cs  |
| `collection`, `collection-patches`       | `TypeName``CollectionName`Patches.cs | GameInterface.Services.`TypeName`s.Patches    | GameInterface\\Services\\`TypeName`s\\Patches\\`TypeName``CollectionName`Patches.cs   |
| `collection`, `collection-messages-created` | `TypeName`Added.cs               | GameInterface.Services.`TypeName`s.Messages      | GameInterface\\Services\\`TypeName`s\\Messages\\Collections\\`CollectionName`Added.cs |
| `collection`, `collection-messages-destroyed`| `TypeName`Removed.cs            | GameInterface.Services.`TypeName`s.Messages      | GameInterface\\Services\\`TypeName`s\\Messages\\Collections\\`CollectionName`Removed.cs|
| `collection`, `collection-messages-create`   | NetworkAdd`TypeName`.cs           | GameInterface.Services.`TypeName`s.Messages      | GameInterface\\Services\\`TypeName`s\\Messages\\Collections\\NetworkAdd`CollectionName`.cs|
| `collection`, `collection-messages-destroy`  | NetworkRemove`TypeName`.cs        | GameInterface.Services.`TypeName`s.Messages      | GameInterface\\Services\\`TypeName`s\\Messages\\Collections\\NetworkRemove`CollectionName`.cs|
| `e2e`, `e2e-props`                          | `TypeName`PropertyTests.cs        | E2E.Tests.Services.`TypeName`s      | E2E.Tests\\Services\\`TypeName`s\\`TypeName`PropertyTests.cs|
| `e2e`, `e2e-fields`                          | `TypeName`FieldTests.cs         | E2E.Tests.Services.`TypeName`s       | E2E.Tests\\Services\\`TypeName`s\\`TypeName`FieldTests.cs|
| `e2e`, `e2e-cols`                          | `TypeName`CollectionTests.cs         | E2E.Tests.Services.`TypeName`s       | E2E.Tests\\Services\\`TypeName`s\\`TypeName`CollectionTests.cs|
| `commands`                          | `TypeName`CollectionTests.cs         | GameInterface.Services.`TypeName`s.Commands;      | GameInterface\\Services\\`TypeName`s\\Commands\\`TypeName`DebugCommands.cs|
