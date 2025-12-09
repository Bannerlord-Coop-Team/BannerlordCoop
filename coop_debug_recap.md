# Récap Debug Coop Bannerlord

## Contexte
- Objectif: faire fonctionner la connexion client/serveur du mod Coop, le chargement de sauvegarde, et l’entrée en campagne sans boucle de pause ou blocage.
- Symptômes initiaux:
  - Surcharge de file réseau avec boucles pause/reprise.
  - `OperationCanceledException` dans la logique de contrôle du temps.
  - Absence de `CampaignReady` côté client, empêchant l’enregistrement des objets (`RegisterAllGameObjects`).
  - `NullReferenceException` dans l’UI de sélection des sauvegardes.

## Journalisation
- Fichiers de logs:
  - Client: `C:\ProgramData\Mount and Blade II Bannerlord\logs\Coop_client.log`
  - Serveur: `C:\ProgramData\Mount and Blade II Bannerlord\logs\Coop_server.log`
  - Copie miroir dans le dossier du module (`Modules\Coop\bin\Win64_Shipping_Client\`): mêmes noms.
- Initialisation logs: `Coop/CoopMod.cs:115–176`.
- Affichage en jeu de certains messages serveur via `InformationManager` filtrés: `Coop/CoopMod.cs:155–174`.

## Changements effectués
- Réseau
  - Augmentation du seuil de surcharge de la file fiable LiteNetLib:
    - `Coop.Core/Common/Configuration/NetworkConfiguration.cs:26` → `MaxPacketsInQueue => 5000`.
  - Timeout de connexion côté DEBUG augmenté à 5 min: `NetworkConfiguration.cs:13`.
- Contrôle du temps
  - Timeout de récupération du mode temps augmenté:
    - `Coop.Core/Server/Services/Time/Handlers/TimeHandler.cs:118` → `CancellationTokenSource(5000)`.
- Patching Harmony
  - Patching automatique à l’activation de `GameInterface` (DI Autofac):
    - `GameInterface/GameInterfaceModule.cs:35–39` → `OnActivated(e => e.Instance.PatchAll())`.
  - Patches `CampaignReady` élargis et tracés:
    - `GameInterface/Services/GameState/Patches/GameLoadedPatch.cs`:
      - Ajout de `OnGameStart` postfix: publication de `CampaignReady`.
      - Ajout de logs `Logger.Information(...)` pour chaque publication (`OnAfterGameInitializationFinished`, `OnGameLoaded`, `OnGameInitializationFinished`, `OnGameStart`).
- UI sauvegardes
  - Durcissement de l’UI pour éviter NRE sur `SaveGroups` et liaisons Gauntlet:
    - Construction sûre des groupes: `GameInterface/Services/UI/LoadGameUI/CoopLoadUI.cs:296–364`.
    - Récupération sûre de `SavedGamesList`: `CoopLoadUI.cs:262–294`.
    - Fallback `FileDriver.GetSaveGameFileInfos()` si `MBSaveLoad.GetSaveFiles()` renvoie vide: `CoopLoadUI.cs:86–99`.
    - Sélection initiale avec mise à jour des propriétés (OnPropertyChanged): `CoopLoadUI.cs:107–141, 143–196`.

## Flux d’initialisation
- Démarrage du module Coop
  - `Coop/CoopMod.cs:112` fixe le thread de boucle de jeu.
  - `Coop/CoopMod.cs:279–291` instancie `Coop.Core.CoopartiveMultiplayerExperience`.
- Démarrage serveur
  - `Coop.Core/CoopartiveMultiplayerExperience.cs:75–100` construit le conteneur (Autofac) et applique Harmony (`IGameInterface.PatchAll`, `IAutoSyncPatchCollector.PatchAll`).
  - `Coop.Core/Server/ServerLogic` passe en `InitialServerState`, ouvre le listener et charge la sauvegarde demandée.
- Démarrage client
  - Réutilise les mêmes registres DI et patches Harmony: `CoopartiveMultiplayerExperience.cs:102–136`.
  - État `LoadingState` s’abonne à `CampaignReady`: `Coop.Core/Client/States/LoadingState.cs:27–45`.
  - Sur `AllGameObjectsRegistered`, bascule vers campagne: `LoadingState.cs:47–62`.
- Publication `CampaignReady`
  - Patches Harmony sur `MBGameManager`: `GameLoadedPatch.cs:17–46` + `OnGameStart`.

## Lecture/écriture de sauvegardes
- Côté serveur: empaquetage et transfert:
  - `GameInterface/Services/Save/Interfaces/SaveInterface.cs:15–43`.
  - Enregistrement des guid objets: `Coop.Core/Server/Services/Save/Handlers/SaveGameHandler.cs:52–77`.
- Côté client: chargement en mémoire et démarrage:
  - `GameInterface/Services/GameState/Interfaces/GameStateInterface.cs:34–55` (chargement via `SaveManager.Load`, démarrage `MBGameManager.StartNewGame`).

## Procédure de test
- Build et tests
  - `dotnet build Coop.sln -v minimal`
  - `dotnet test Coop.sln -v minimal`
- En jeu
  - Ouvrir l’UI d’hébergement (`JoinWindow` → `CoopLoadGameGauntletScreen`).
  - Sélectionner une sauvegarde et héberger.
  - Surveiller `Coop_client.log` / `Coop_server.log`:
    - Chercher les lignes `Publishing CampaignReady (...)`.
    - Vérifier `RegisterAllGameObjects` puis `AllGameObjectsRegistered`, et l’entrée dans `CampaignState`.
  - Vérifier absence de boucles pause/reprise et absence d’exception UI.

## Points d’attention
- Si `CampaignReady` n’apparaît pas:
  - Confirmer que `GameInterface.PatchAll` est appelé avant `MBGameManager.StartNewGame` (DI `OnActivated` et `CoopartiveMultiplayerExperience` assurent cela).
  - Vérifier collisions de patches avec d’autres modules.
- En cas de surcharge réseau persistante:
  - Ajuster `MaxPacketsInQueue`.
  - Vérifier `PeerQueueOverloadedHandler` côté serveur.

## Fichiers clés et lignes
- Patching auto: `GameInterface/GameInterfaceModule.cs:35–39`.
- Événements `CampaignReady`: `GameInterface/Services/GameState/Patches/GameLoadedPatch.cs:17–46, +OnGameStart`.
- Client `LoadingState`: `Coop.Core/Client/States/LoadingState.cs:41–62`.
- Régistries: `GameInterface/Registry/Handlers/RegistryHandler.cs:27–35`.
- UI sauvegardes durcie: `GameInterface/Services/UI/LoadGameUI/CoopLoadUI.cs:262–364`.
- TimeHandler timeout: `Coop.Core/Server/Services/Time/Handlers/TimeHandler.cs:118–133`.
- Network config: `Coop.Core/Common/Configuration/NetworkConfiguration.cs:12–33`.

## Commandes utiles
- Build solution: `dotnet build Coop.sln -v minimal`
- Tests unitaires: `dotnet test Coop.sln -v minimal`
- Logs: ouvrir les `.log` sous `C:\ProgramData\Mount and Blade II Bannerlord\logs`.


