# Coop – Récap debug

## Catégories XML activées (SubModule.xml)
- Items → `spitems`
- NPCCharacters → `characters`
- Settlements → `settlements`
- GameText → `module_strings`
- CraftingPieces → `new_pieces`
- WeaponDescriptions → `new_weapon_descriptions`
- CraftingTemplates → `new_templates`
- BodyProperties → `BodyProperties`

## Fichiers ModuleData présents
- `ModuleData/spitems/spitems.xml` (racine `<Items>`, item minimal `coop_debug_token`)
- `ModuleData/characters/characters.xml` (racine `<NPCCharacters>`, NPC minimal `coop_debug_npc`)
- `ModuleData/module_strings.xml` (racine `<strings>`, entrée `coop_debug_label`)
- `ModuleData/settlements.xml` (racine `<Settlements>`, vide)
- `ModuleData/new_pieces.xml` (racine `<CraftingPieces>`, vide)
- `ModuleData/new_templates.xml` (racine `<CraftingTemplates>`, vide)
- `ModuleData/new_weapon_descriptions.xml` (racine `<WeaponDescriptions>`, vide)
- `ModuleData/BodyProperties/BodyProperties.xml` (racine `<BodyProperties>`, vide)

## Dossiers prêts pour contenus futurs
- `AssetPackages`, `Prefabs`, `SceneObj`, `Sounds`, `Music`, `GUI/Brushes`
- `ModuleData/Languages`, `ModuleData/troops`, `ModuleData/troop_upgrade_tools`

## Build
- Commande: `dotnet build BannerlordCoop/source/Coop.sln -c Release`
- État: succès (2 avertissements)
- DLLs disponibles dans `Modules/Coop/bin/Win64_Shipping_Client/` (incl. `Coop.dll`, `GameInterface.dll`)

## Notes
- Les nouvelles catégories sont déclarées et prêtes, sans impact gameplay tant qu’aucun contenu custom n’est ajouté.
