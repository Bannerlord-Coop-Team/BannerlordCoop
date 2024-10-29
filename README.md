# Bannerlord Coop - Scaffolding
A dotnet CLI scaffolding tool for the Bannerlord Coop project using razor.

#### **Do not merge this branch**
This branch is meant exclusively for developing the CLI tool, and its not a separate repository only for convinience

## Usage

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
## Summary
These are the files the CLI can generate

| Command                    | Output Path                                                                 | Namespace                                         |
|--------------------------|------------------------------------------------------------------------------|--------------------------------------------------|
| registry                 | Gameinterface\\Services\\`TypeName`s\\`TypeName`Registry.cs                 | GameInterface.Services.`TypeName`s               |
| AutoSync                 | Gameinterface\\Services\\`TypeName`s\\`TypeName`Sync.cs                     | GameInterface.Services.`TypeName`s               |
| LifeTimeHandler          | Gameinterface\\Services\\`TypeName`s\\Handlers\\`TypeName`LifetimeHandler.cs| GameInterface.Services.`TypeName`s.Handlers      |
| LifeTimePatches          | Gameinterface\\Services\\`TypeName`s\\Patches\\`TypeName`LifetimePatches.cs | GameInterface.Services.`TypeName`s.Patches       |
| CreatedMessage           | Gameinterface\\Services\\`TypeName`s\\Messages\\Lifetime\\`TypeName`Created.cs | GameInterface.Services.`TypeName`s.Messages    |
| DestroyedMessage         | Gameinterface\\Services\\`TypeName`s\\Messages\\Lifetime\\`TypeName`Destroyed.c | GameInterface.Services.`TypeName`s.Messages   |
| NetworkCreateMessage     | Gameinterface\\Services\\`TypeName`s\\Messages\\Lifetime\\NetworkCreate`TypeName`.cs | GameInterface.Services.`TypeName`s.Messages |
| NetworkDestroyMessage    | Gameinterface\\Services\\`TypeName`s\\Messages\\Lifetime\\NetworkDestroy`TypeName`.cs| GameInterface.Services.`TypeName`s.Messages |
| CollectionHandler        | Gameinterface\\Services\\`TypeName`s\\Handlers\\`CollectionName`Handler.cs  | GameInterface.Services.`TypeName`s.Handlers      |
| CollectionPatches        | Gameinterface\\Services\\`TypeName`s\\Patches\\`CollectionName`Patches.cs   | GameInterface.Services.`TypeName`s.Patches       |
| CollectionAddedMessage   | Gameinterface\\Services\\`TypeName`s\\Messages\\Collections\\`CollectionName`Added.cs | GameInterface.Services.`TypeName`s.Messages |
| CollectionRemovedMessage | Gameinterface\\Services\\`TypeName`s\\Messages\\Collections\\`CollectionName`Removed.cs | GameInterface.Services.`TypeName`s.Messages |
| CollectionNetworkAddMessage | Gameinterface\\Services\\`TypeName`s\\Messages\\Collections\\NetworkAdd`CollectionName`.cs | GameInterface.Services.`TypeName`s.Messages |
| CollectionNetworkRemoveMessage | Gameinterface\\Services\\`TypeName`s\\Messages\\Collections\\NetworkRemove`CollectionName`.cs | GameInterface.Services.`TypeName`s.Messages |

## Commands

### Working

- **`registry <type-fully-qualified-name> [--overwrite]`**  
  Generates a registry class for the specified type.

- **`sync <type-fully-qualified-name> [--members] [--overwrite]`**  
  Generates a sync class for the specified type. Use `--members` to include member synchronization.

- **`lifetime <type-fully-qualified-name> [--overwrite]`**  
  Generates a lifetime handler for the specified type.

### WIP

- **`commands`**  
  Generates command-related classes (under development).

- **`collections`**  
  Generates collection-related classes (under development).

- **`e2e`**  
  Generates end-to-end tests (under development).

## Options

- **`--overwrite`**: Overwrites existing files.
- **`--members`**: (For sync command) Includes member synchronization.
