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

### Châteaux / Encounters
- Gestion `StartSettlementEncounter` côté jeu:
  - Détection d’un `PlayerEncounter.Current` déjà actif: `BannerlordCoop/source/GameInterface/Services/MobileParties/Handlers/SettlementExitEnterHandler.cs:143–147`.
  - Filtrage des actions non‑joueur (parties IA): `SettlementExitEnterHandler.cs:149–158`.
  - Résolution sûre des objets et initialisations manquantes:
    - `Settlement.InitSettlement()` si `settlement.Party` est null: `SettlementExitEnterHandler.cs:167–176`.
    - `MobileParty.OnFinishLoadState()` si `mobileParty.Party` est null: `SettlementExitEnterHandler.cs:178–187`.
  - Démarrage Encounter:
    - Tentative via `EncounterManager.StartSettlementEncounter`: `SettlementExitEnterHandler.cs:216–218`.
    - Fallback robuste: `PlayerEncounter.Start()` puis `PlayerEncounter.Current.Init(...)`: `SettlementExitEnterHandler.cs:224–233`.
- Hydratation des objets à `CampaignReady`:
  - Enregistrement de tous les `Settlement`, `MobileParty` et `PartyBase` existants avec comptage des doublons: `SettlementExitEnterHandler.cs:254–287`.
- Filtrage des événements réseau côté client pour éviter les entrées/sorties du joueur en doublon:
  - Ignore `NetworkPartyEnterSettlement`/`Leave` lorsque `payload.PartyId == "player_party"`: `BannerlordCoop/source/Coop.Core/Client/Services/MobileParties/Handlers/ClientSettlementExitEnterHandler.cs:80–83, 91–94`.
- Durcissement des registres pour prévenir les clés nulles:
  - Journalisation de la stack trace lors d’échec d’ajout dans `ConditionalWeakTable`: `BannerlordCoop/source/GameInterface/Registry/RegistryBase.cs:107–108, 132–133`.

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
  - Tester l’entrée dans un château:
    - Observer les logs `StartSettlementEncounter reçu ...` puis `EncounterManager.StartSettlementEncounter ok` ou le fallback `PlayerEncounter.Init ok`.
    - Vérifier que le menu du château s’affiche sans crash.
    - Contrôler qu’aucun doublon d’événement `player_party` ne perturbe le flux (côté client filtré).

## Points d’attention
- Si `CampaignReady` n’apparaît pas:
  - Confirmer que `GameInterface.PatchAll` est appelé avant `MBGameManager.StartNewGame` (DI `OnActivated` et `CoopartiveMultiplayerExperience` assurent cela).
  - Vérifier collisions de patches avec d’autres modules.
- En cas de surcharge réseau persistante:
  - Ajuster `MaxPacketsInQueue`.
  - Vérifier `PeerQueueOverloadedHandler` côté serveur.
- Si `PlayerEncounter` est déjà actif sur un mauvais `Settlement` (cas observé rare):
  - Re‑initialiser l’Encounter en liant `mobileParty.Party` et `settlement.Party` corrects (plan de durcissement en cours; actuellement, la logique ignore le démarrage si un Encounter est actif).

## Fichiers clés et lignes
- Patching auto: `GameInterface/GameInterfaceModule.cs:35–39`.
- Événements `CampaignReady`: `GameInterface/Services/GameState/Patches/GameLoadedPatch.cs:17–46, +OnGameStart`.
- Client `LoadingState`: `Coop.Core/Client/States/LoadingState.cs:41–62`.
- Régistries: `GameInterface/Registry/Handlers/RegistryHandler.cs:27–35`.
- UI sauvegardes durcie: `GameInterface/Services/UI/LoadGameUI/CoopLoadUI.cs:262–364`.
- TimeHandler timeout: `Coop.Core/Server/Services/Time/Handlers/TimeHandler.cs:118–133`.
- Network config: `Coop.Core/Common/Configuration/NetworkConfiguration.cs:12–33`.
- Entrée/sortie de settlement et Encounter: `GameInterface/Services/MobileParties/Handlers/SettlementExitEnterHandler.cs:137–243`.
- Filtrage événements réseau client: `Coop.Core/Client/Services/MobileParties/Handlers/ClientSettlementExitEnterHandler.cs:77–99`.
- Registre objets et stack trace sur clés nulles: `GameInterface/Registry/RegistryBase.cs:53–139`.

## Commandes utiles
- Build solution: `dotnet build Coop.sln -v minimal`
- Tests unitaires: `dotnet test Coop.sln -v minimal`
- Logs: ouvrir les `.log` sous `C:\ProgramData\Mount and Blade II Bannerlord\logs`.

## Mises à jour récentes (post-dernière mise à jour GitHub)
- Correction crash “Se connecter” par séquencement retour menu → chargement:
  - `Coop.Core\Client\States\ReceivingSavedDataState.cs:44–58` publie `EnterMainMenu` dès réception de la sauvegarde (au lieu de charger immédiatement).
  - `Coop.Core\Client\States\ReceivingSavedDataState.cs:60–76` charge la sauvegarde après `MainMenuEntered` et bascule en `LoadingState`.
- Gardes et chargement bloquant côté jeu:
  - `GameInterface\Services\GameState\Interfaces\GameStateInterface.cs:24–43` garde `IsLoadingGame` et fenêtre `EnterMainMenuBlockedUntil`.
  - `GameInterface\Services\GameState\Interfaces\GameStateInterface.cs:45–50` chargement bloquant sur thread principal via `GameLoopRunner`.
  - `GameInterface\Services\GameState\Interfaces\GameStateInterface.cs:52–80` `SaveManager.Load` + `MBGameManager.StartNewGame` + publication `CampaignReady`.
- Flux UI Join validé:
  - `Coop\CoopMod.cs:478–500` publie `AttemptJoin` puis ferme la couche UI; l’orchestrateur démarre le client.
- Documentation en ligne (commentaires) ajoutée pour accélérer l’onboarding:
  - `Coop\Core\Client\States\ValidateModuleState.cs:31–51, 61–74, 76–91, 93–100, 136–139` (validation modules, branchement héros vs création).
  - `Common\GameLoopRunner.cs:32–55, 64–84, 86–90` (exécution main-thread, file d’actions, blocage optionnel).
  - `GameInterface\Services\CharacterCreation\Handlers\CharacterCreationHandler.cs:7–31` (pipeline de création de personnage).
  - `Coop\Core\CoopartiveMultiplayerExperience.cs:49–62, 80–105, 106–137, 139–145` (orchestration DI client/serveur, patchs Harmony).
  - `Coop.Core\Server\Connections\States\ResolveCharacterState.cs:54–73, 75–84, 86–93, 95–102` (validation modules, résolution héros, transfert/creation).
  - `Coop.Core\Client\CoopClient.cs:55–65, 72–76, 78–95, 97–106, 117–155, 188–211` (ping UDP, Connect, reconnexions, routage paquets).
  - `Coop.Core\Server\CoopServer.cs:63–68, 118–131, 156–172, 186–201, 203–209` (NAT punch, ping/pong, acceptation connexions, surcharge file).
  - `GameInterface\Services\GameState\Handlers\EnterMainMenuHandler.cs:12–18, 25–39` (signal `MainMenuEntered`).
- Build Release validé après modifications:
  - `dotnet build Coop.sln -c Release` OK (un avertissement mineur `using System.IO` dupliqué dans `Coop\CoopMod.cs`).


