# Player-visible behaviors

Generated from [catalog.csv](catalog.csv) by `python tools/feature_map.py refresh`.
Material outcome branches are retained in [source-cases.csv](source-cases.csv).
Oracles below are acceptance requirements, not recorded passing results. A candidate is a coverage question;
neither its presence nor a nearby declaration establishes the feature's exact installed behavior or co-op support.
Source-inspected rows identify repository surfaces only. See [evidence](evidence/authoring.md) for actual exercise.

## sessions

Entry: Coop menu / connection workflow

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| sessions.001 | Start authoritative server | server launch; no player hero/party on host | server campaign reaches readiness without consuming a client slot | unverified / source-inspected |
| sessions.002 | Join a server | server address; Steam selection; explicit join | client obtains its own registered hero and party | unverified / source-inspected |
| sessions.003 | Password admission | correct; incorrect; changed during reconnect | admission decision agrees with configured server password | unverified / source-inspected |
| sessions.004 | Module admission | matching modules; mismatched module set or version | mismatch is reported before incompatible campaign state is applied | unverified / source-inspected |
| sessions.005 | Save transfer | initial join; interrupted transfer; late join | client loads the server campaign identity before gameplay becomes ready | unverified / source-inspected |
| sessions.006 | Player identity | new player; existing saved registration | the same player resolves to the intended hero and party after rejoin | unverified / source-inspected |
| sessions.007 | Second client admission | two different platform identities; repeated join | two clients are admitted with distinct player parties | unverified / source-inspected |
| sessions.008 | Disconnect player | normal leave; lost connection; crash | remaining peers observe the intended player/party state | unverified / source-inspected |
| sessions.009 | Reconnect player | same campaign; while other client continues | rejoining client converges without duplicate registered world objects | unverified / source-inspected |
| sessions.010 | Join during campaign action | AI movement; settlement change; ongoing encounter | initial state and subsequent updates have a consistent order | unverified / source-inspected |
| sessions.011 | Server shutdown | idle; clients connected; pending save | owned shutdown retains the expected save and terminates owned processes | unverified / source-inspected |
| sessions.012 | Connection error presentation | unreachable server; rejected join; partial startup | visible reason agrees with the returned failure and readiness state | unverified / source-inspected |

Sources: [source/Coop.Core/Client](../source/Coop.Core/Client), [source/Coop.Core/Server](../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../source/Coop/CoopMod.cs)

## character

Entry: Client character creation / character screen

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| character.001 | Create player character | first admission; returning player | new character belongs to exactly one admitted player | unverified / candidate |
| character.002 | Select culture | available cultures; saved culture | chosen culture survives replication and reload | unverified / candidate |
| character.003 | Choose background | childhood; adolescence; adulthood choices | selected background produces the intended initial development state | unverified / candidate |
| character.004 | Choose body appearance | face; hair; age; build; weight; sex | own character appearance matches on both rendered clients | unverified / candidate |
| character.005 | Name hero | initial name; name edit; localized characters | name is visible consistently in campaign and character UI | unverified / candidate |
| character.006 | Name clan | initial clan name; rename | clan name converges without changing clan identity | unverified / candidate |
| character.007 | Edit banner | colors; emblem; saved banner | party and mission banner use the selected design | unverified / candidate |
| character.008 | Initial equipment | battle; civilian; stealth equipment | correct equipment is used in each applicable context | unverified / candidate |
| character.009 | Create starting party | registration; ownership; initial roster | the client has one controllable party with resolvable network IDs | unverified / candidate |
| character.010 | Character creation cancellation | before confirmation; reconnect during creation | cancellation does not leave a second hero or party | unverified / candidate |

Sources: [source/GameInterface/Services/Banners](../source/GameInterface/Services/Banners), [source/GameInterface/Services/CharacterCreation](../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../source/GameInterface/Services/Heroes)

## movement

Entry: Client campaign map

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| movement.001 | Move to map position | short travel; long travel; unreachable destination | authoritative position reaches the selected reachable destination | unverified / candidate |
| movement.002 | Move to settlement | town; castle; village; hostile settlement | party reaches the correct settlement and entry state | unverified / candidate |
| movement.003 | Follow another party | own companion; allied party; another player | follow target identity remains stable as the target moves | unverified / candidate |
| movement.004 | Escort party | escort begins; target changes; escort ends | escort behavior and resulting movement agree on all peers | unverified / candidate |
| movement.005 | Patrol map region | patrol point; repeated AI tick | party changes movement according to the authoritative patrol state | unverified / candidate |
| movement.006 | Flee another party | enemy approach; danger passes | escape target and movement state converge | unverified / candidate |
| movement.007 | Wait on campaign map | stationary; enemy encounter while waiting | time passes under server policy and encounters interrupt correctly | unverified / candidate |
| movement.008 | Stop movement | manual stop; target removed | party stops without a stale movement order being reapplied | unverified / candidate |
| movement.009 | Encounter moving party | friendly; neutral; enemy; player party | all peers agree on encounter participants and action availability | unverified / candidate |
| movement.010 | Map movement speed | food; morale; terrain; day/night; load | observed travel rate follows the applicable model and current party state | unverified / candidate |
| movement.011 | Map visibility | nearby; outside sight; hidden party; late join | each player sees the permitted authoritative party state | unverified / candidate |
| movement.012 | Map tracks | track creation; age; followed party | track identity and presentation match the observed campaign activity | unverified / candidate |
| movement.013 | Settlement exit | ordinary leave; hostile escape; menu transition | party exits the correct settlement and can move afterward | unverified / candidate |
| movement.014 | Destroy movement target | target party removed; settlement context changes | movement releases the dead target without registry lookup failure | unverified / candidate |

Sources: [source/GameInterface/Services/MapTracks](../source/GameInterface/Services/MapTracks), [source/GameInterface/Services/MobileParties](../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../source/GameInterface/Services/MobilePartyAIs)

## party

Entry: Client party screen / campaign map

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| party.001 | Recruit troops | town; village; mercenary; volunteer slots | roster increases once and gold/volunteer stock decreases accordingly | unverified / candidate |
| party.002 | Upgrade troops | eligible; insufficient XP; insufficient gold; mount requirement | chosen upgrade consumes the correct resources and preserves total troop count | unverified / candidate |
| party.003 | Transfer troops | garrison; companion party; dungeon context | source and destination rosters balance after the accepted transfer | unverified / candidate |
| party.004 | Dismiss troops | one; stack; wounded troops | selected troops leave once and roster totals remain consistent | unverified / candidate |
| party.005 | Reorder party roster | hero; troop stack; formation preference | saved roster ordering matches the player's selection | unverified / candidate |
| party.006 | Inspect troop details | healthy; wounded; XP; upgrade targets | UI totals agree with authoritative roster state | unverified / candidate |
| party.007 | Recruit prisoners | available; insufficient conformity; hero prisoner | only permitted prisoners become troops and prisoner totals decrease | unverified / candidate |
| party.008 | Ransom prisoners | ordinary troops; hero; selected stack | gold and prisoner removal reflect one completed ransom | unverified / candidate |
| party.009 | Transfer prisoners | party to dungeon; dungeon to party | both prisoner rosters conserve the transferred count | unverified / candidate |
| party.010 | Troop healing | wounded; settlement rest; surgeon role | healthy/wounded totals follow observed healing ticks | unverified / candidate |
| party.011 | Troop casualties | battle; simulation; raid; starvation | casualty types and resulting roster counts converge | unverified / candidate |
| party.012 | Party food consumption | food present; no food; multiple food types | consumed items and party food state agree after the tick | unverified / candidate |
| party.013 | Party morale | food diversity; wages; battles; recent events | morale and consequences match the applicable party state | unverified / candidate |
| party.014 | Pay party wages | sufficient funds; insufficient funds; daily tick | gold and wage-related consequences occur once per authoritative tick | unverified / candidate |
| party.015 | Party capacity | below cap; at cap; over cap | displayed limit and overcrowding consequences match the current model | unverified / candidate |
| party.016 | Party trade gold | trade; daily income; gold transfer | party funds converge separately from hero funds | unverified / candidate |
| party.017 | Party role assignment | scout; surgeon; engineer; quartermaster | assigned hero and effective role are consistent after replacement | unverified / candidate |
| party.018 | Create companion party | eligible companion; party limit | new party has one owner and registered component/rosters | unverified / candidate |
| party.019 | Disband companion party | travelling; returning; owner change | party reaches the correct disband state without a zombie party | unverified / candidate |
| party.020 | Party wage limit | limit edit; automatic recruitment | wage limit controls recruitment as the source specifies | unverified / candidate |

Sources: [source/GameInterface/Services/MobileParties](../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/Party](../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../source/GameInterface/Services/TroopRosters)

## heroes

Entry: Hero / companion / family interfaces

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| heroes.001 | Change hero gold | reward; purchase; transfer; loss | hero gold changes once and agrees on both clients | unverified / candidate |
| heroes.002 | Change hero state | active; wounded; prisoner; dead; disabled | state transitions and campaign membership remain consistent | unverified / candidate |
| heroes.003 | Hire companion | tavern conversation; fee; party full | hired hero joins the intended clan and party after payment | unverified / candidate |
| heroes.004 | Dismiss companion | conversation; clan screen | dismissed hero leaves only the owning player's clan/party | unverified / candidate |
| heroes.005 | Reassign companion | party; governor; formation captain | old assignment is removed and new assignment becomes authoritative | unverified / candidate |
| heroes.006 | Hero location | party; settlement; location scene | hero appears in the correct campaign and mission location | unverified / candidate |
| heroes.007 | Hero relationship | positive action; negative action; owner-specific reward | relationship updates reach the intended player hero | unverified / candidate |
| heroes.008 | Court hero | dialogue stages; persuasion success/failure | romance progress belongs to the correct player and partner | unverified / candidate |
| heroes.009 | Marriage proposal | eligible; ineligible; barter payment | marriage and clan changes occur once after accepted terms | unverified / candidate |
| heroes.010 | Marriage offer response | accept; reject; expired offer | only the addressed player's response affects the offer | unverified / candidate |
| heroes.011 | Pregnancy and childbirth | daily tick; parent identity; newborn registration | parentage and new hero identity converge | unverified / candidate |
| heroes.012 | Child education | education stage; choice; deferred choice | selected development affects the intended child | unverified / candidate |
| heroes.013 | Hero aging | campaign time; age milestone | age-dependent state agrees after the authoritative time advance | unverified / candidate |
| heroes.014 | Hero death | battle death; execution; aging | death state, relationships and succession side effects converge | unverified / candidate |
| heroes.015 | Hero execution | permitted prisoner; cancelled confirmation | accepted execution affects the selected prisoner once | unverified / candidate |
| heroes.016 | Retirement and heir | retire; hero death; heir selection | controlled player identity transfers to the intended eligible heir | unverified / candidate |
| heroes.017 | Hero notifications | skills; marriage; death; clan change | notification refers to the affected hero and appears in the proper client context | unverified / candidate |

Sources: [source/GameInterface/Services/Actions](../source/GameInterface/Services/Actions), [source/GameInterface/Services/Companions](../source/GameInterface/Services/Companions), [source/GameInterface/Services/Heroes](../source/GameInterface/Services/Heroes)

## progression

Entry: Client character development screen

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| progression.001 | Gain skill XP | combat; trade; healing; campaign roles | earned XP reaches the intended hero rather than another player's hero | unverified / candidate |
| progression.002 | Increase skill level | below threshold; cross threshold; multiple levels | skill value and available perk choices agree after XP is applied | unverified / candidate |
| progression.003 | Spend attribute point | available point; no points; repeated click | one accepted choice consumes one point | unverified / candidate |
| progression.004 | Spend focus point | available point; learning limit; repeated click | focus and remaining points agree on all peers | unverified / candidate |
| progression.005 | Select perk | eligible; alternative already selected; insufficient skill | one valid perk selection is retained and reflected in the UI | unverified / candidate |
| progression.006 | Apply personal perk effect | each indexed perk with Personal role | measure the relevant personal action against an independent expected bonus | unverified / candidate |
| progression.007 | Apply party leader perk effect | each indexed perk with PartyLeader role | measure the owning party effect without affecting another player's party | unverified / candidate |
| progression.008 | Apply captain perk effect | captain assigned; replaced; formation troops | only the eligible formation receives the documented effect | unverified / candidate |
| progression.009 | Apply governor perk effect | governor assigned; replaced; no governor | only the governed settlement receives the documented effect | unverified / candidate |
| progression.010 | Apply clan leader perk effect | leader; nonleader; leader replacement | clan-level effect follows the active eligible leader | unverified / candidate |
| progression.011 | Reset perks | arena master; payment; confirm/cancel | reset removes eligible choices and payment occurs only for the accepted action | unverified / candidate |
| progression.012 | Trait progression | reward; betrayal; relation consequence | trait XP/state belongs to the correct player and survives reload | unverified / candidate |
| progression.013 | Display learning limit | attributes; focus; skill change | displayed learning state agrees with the authoritative development state | unverified / candidate |
| progression.014 | Apply Scout perk effect | scout assigned; scout replaced; no eligible scout | Observe this declared Scout effect only in its actual eligible consumer context; no other role inherits it | unverified / candidate |
| progression.015 | Apply Engineer perk effect | engineer assigned; engineer replaced; no eligible engineer | Observe this declared Engineer effect only in its actual eligible consumer context; no other role inherits it | unverified / candidate |
| progression.016 | Apply Quartermaster perk effect | quartermaster assigned; quartermaster replaced; no eligible quartermaster | Observe this declared Quartermaster effect only in its actual eligible consumer context; no other role inherits it | unverified / candidate |
| progression.017 | Apply Surgeon perk effect | surgeon assigned; surgeon replaced; no eligible surgeon | Observe this declared Surgeon effect only in its actual eligible consumer context; no other role inherits it | unverified / candidate |
| progression.018 | Apply ArmyCommander perk effect | army commander; army member; army leadership changes | Observe this declared ArmyCommander effect only in its actual eligible consumer context; no other role inherits it | unverified / candidate |
| progression.019 | Apply PartyMember perk effect | perk holder present; holder absent; holder in another party | Observe this declared PartyMember effect only in its actual eligible consumer context; no other role inherits it | unverified / candidate |

Sources: [features/baseline/installed.json](../features/baseline/installed.json), [source/GameInterface/Services/CharacterDevelopers](../source/GameInterface/Services/CharacterDevelopers), [source/GameInterface/Services/HeroDevelopers](../source/GameInterface/Services/HeroDevelopers)

## inventory

Entry: Client inventory / equipment screen

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| inventory.001 | Equip weapon | empty slot; replacement; two-handed; shield | selected item appears in the correct equipment slot and mission | unverified / candidate |
| inventory.002 | Equip armor | head; body; arm; leg; cape | armor and resulting character appearance match both clients | unverified / candidate |
| inventory.003 | Equip mount | horse; camel; incompatible harness; no mount | mounted state uses the selected valid equipment combination | unverified / candidate |
| inventory.004 | Equip civilian clothing | town scene; civilian weapon restrictions | civilian loadout is preserved separately from battle equipment | unverified / candidate |
| inventory.005 | Equip stealth loadout | stealth mission; return to ordinary scene | context uses the correct loadout and restores ordinary equipment afterward | unverified / candidate |
| inventory.006 | Move inventory item | single; stack; modifier variant | item identities and quantities balance across moved stacks | unverified / candidate |
| inventory.007 | Discard item | confirm; cancel; whole stack | discard removes only the confirmed quantity | unverified / candidate |
| inventory.008 | Sort inventory | name; type; value; weight | ordering changes presentation without changing ownership or quantities | unverified / candidate |
| inventory.009 | Inventory capacity | pack animals; heavy load; overburden | weight and movement consequences match roster/equipment state | unverified / candidate |
| inventory.010 | Item modifiers | damaged; fine; crafted; replacement | modifier identity survives transfer and equipment replication | unverified / candidate |
| inventory.011 | Use consumable resources | food; crafting material; ammunition | the production action consumes the correct item quantity | unverified / candidate |
| inventory.012 | Loot inventory | battle loot; raid loot; declined items | accepted items reach the owning player's inventory once | unverified / candidate |

Sources: [source/GameInterface/Services/Equipments](../source/GameInterface/Services/Equipments), [source/GameInterface/Services/Inventory](../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../source/GameInterface/Services/ItemRosters)

## trade

Entry: Town/village trade screen / barter conversation

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| trade.001 | Buy goods | one item; stack; insufficient gold | buyer gold decreases and market/player item counts balance | unverified / candidate |
| trade.002 | Sell goods | one item; stack; merchant funds exhausted | seller gold and market stock change by the completed transaction | unverified / candidate |
| trade.003 | Trade cancellation | before Done; disconnect; leave screen | cancelled selection does not change final authoritative inventories | unverified / candidate |
| trade.004 | Trade price | scarcity; category; modifier; skill influence | quoted and charged price agree for the accepted item selection | unverified / candidate |
| trade.005 | Concurrent market trade | two clients purchase the same stock | stock is conserved and each accepted transaction is applied once | unverified / candidate |
| trade.006 | Trade XP | profitable sale; nonprofitable sale; equipment sale | eligible XP/reward belongs to the player who completed the sale | unverified / candidate |
| trade.007 | Trade rumors | new rumor; changed market; expired rumor | the relevant player's rumor data and UI agree | unverified / candidate |
| trade.008 | Barter gold | one-sided; two-sided; cancel | accepted gold transfer balances both participating heroes | unverified / candidate |
| trade.009 | Barter items | stacks; modifiers; insufficient quantity | accepted item transfer conserves identities and counts | unverified / candidate |
| trade.010 | Barter prisoners | hero; ordinary prisoner; unavailable captive | only accepted transferable captives change owner | unverified / candidate |
| trade.011 | Barter fief | eligible; restricted; cancel | accepted ownership change triggers the required settlement/clan effects | unverified / candidate |
| trade.012 | Barter agreement | peace; marriage; join faction | agreement side effects occur only after accepted terms | unverified / candidate |

Sources: [source/GameInterface/Services/Barters](../source/GameInterface/Services/Barters), [source/GameInterface/Services/Inventory](../source/GameInterface/Services/Inventory), [source/GameInterface/Services/TownMarketDatas](../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../source/GameInterface/Services/VillageMarketDatas)

## workshops

Entry: Town workshop conversation / clan finance

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| workshops.001 | Purchase workshop | available seller; ownership limit; insufficient gold | payment and workshop owner change occur once | unverified / candidate |
| workshops.002 | Sell workshop | owned; unowned; ownership changed | sale proceeds reach the intended owner and ownership updates | unverified / candidate |
| workshops.003 | Change workshop production | valid production type; payment | production type and inventory/cost effects converge | unverified / candidate |
| workshops.004 | Workshop production tick | inputs available; unavailable; storage | input consumption and output stock follow the authoritative tick | unverified / candidate |
| workshops.005 | Workshop income | profitable; unprofitable; daily settlement | income belongs to the correct owning hero/clan | unverified / candidate |
| workshops.006 | Workshop destruction or confiscation | war; settlement capture; lost ownership | ownership and subsequent income stop/change consistently | unverified / candidate |

Sources: [source/GameInterface/Services/Actions](../source/GameInterface/Services/Actions), [source/GameInterface/Services/Workshops](../source/GameInterface/Services/Workshops)

## caravans

Entry: Notable conversation / clan parties / campaign map

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| caravans.001 | Create caravan | leader choice; normal/strong escort; fee | one registered caravan is created for the intended owner | unverified / candidate |
| caravans.002 | Caravan trading | visit market; buy; sell | caravan stock, funds and owner income follow the authoritative transaction | unverified / candidate |
| caravans.003 | Caravan movement | destination choice; route; threat response | all peers observe the same caravan identity and relevant position | unverified / candidate |
| caravans.004 | Caravan capture | battle defeat; prisoners; lost inventory | caravan removal and leader captivity converge | unverified / candidate |
| caravans.005 | Disband caravan | owner action; travelling; in settlement | caravan and owner accounting reach the intended final state | unverified / candidate |
| caravans.006 | Caravan replacement after loss | old leader captive; new leader | replacement does not reuse a live party identity | unverified / candidate |

Sources: [source/GameInterface/Services/Caravans](../source/GameInterface/Services/Caravans), [source/GameInterface/Services/MobileParties](../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/PartyComponents](../source/GameInterface/Services/PartyComponents)

## crafting

Entry: Town smithy / crafting screen

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| crafting.001 | Choose smith | player; companion; unavailable smith | crafting changes and stamina apply to the selected hero | unverified / candidate |
| crafting.002 | Select weapon template | type; valid pieces; incompatible pieces | selected combination resolves to a valid weapon design | unverified / candidate |
| crafting.003 | Change weapon pieces | blade; guard; handle; pommel; size | crafted design retains the exact selected compatible parts | unverified / candidate |
| crafting.004 | Forge weapon | materials available; missing material; insufficient stamina | one weapon is created with correct resource/stamina consumption | unverified / candidate |
| crafting.005 | Name crafted weapon | default name; custom name; localized text | name and crafted item identity survive replication/reload | unverified / candidate |
| crafting.006 | Refine material | valid conversion; missing input; perk variant | resource quantities balance according to the selected conversion | unverified / candidate |
| crafting.007 | Smelt weapon | ordinary; crafted; modifier; no stamina | weapon is consumed and recovered materials match the action | unverified / candidate |
| crafting.008 | Unlock crafting piece | new unlock; already unlocked | unlock state belongs to the correct development context | unverified / candidate |
| crafting.009 | Complete crafting order | requirements met; missed quality; deadline | reward and order state reflect the completed crafted weapon | unverified / candidate |
| crafting.010 | Crafting stamina recovery | rest; campaign travel; hero switched | recovery follows time and the selected hero's record | unverified / candidate |
| crafting.011 | Crafted item registration | new item; repeat same design; reload | all peers resolve the same crafted item and design identities | unverified / candidate |

Sources: [source/GameInterface/Services/CraftingOrders](../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/CraftingService](../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/Smithing](../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../source/GameInterface/Services/WeaponDesigns)

## towns

Entry: Town/castle menu / settlement management

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| towns.001 | Enter town | friendly; neutral; hostile; siege | menu and encounter state reflect the actual settlement access | unverified / candidate |
| towns.002 | Enter castle | friendly; permission denied; hostile | correct castle access and interior options are shown | unverified / candidate |
| towns.003 | Wait in settlement | start; stop; interrupted by siege | time and party/settlement state agree when waiting ends | unverified / candidate |
| towns.004 | Inspect settlement ownership | town; castle; capture | owner clan/kingdom and map presentation agree | unverified / candidate |
| towns.005 | Change governor | assign; replace; remove; hero unavailable | old/new governor assignments and settlement effects converge | unverified / candidate |
| towns.006 | Manage garrison roster | deposit; withdraw; upgrade | party and garrison troop counts balance | unverified / candidate |
| towns.007 | Garrison component backlink | initialize; finalize; repeated lifecycle | settlement references the correct registered active garrison component | unverified / source-inspected |
| towns.008 | Automatic garrison recruitment | enabled; disabled; wage limit | recruits appear only under the permitted authoritative policy | unverified / candidate |
| towns.009 | Inspect militia | growth; veteran ratio; casualties | militia roster/state agree across peers | unverified / candidate |
| towns.010 | Town prosperity | daily growth; loss; starvation | prosperity and related economy state change once per tick | unverified / candidate |
| towns.011 | Town loyalty | governor; culture; policies; starvation | loyalty and rebellion conditions use the authoritative settlement state | unverified / candidate |
| towns.012 | Town security | garrison; bandits; policies; low security | security and dependent state converge | unverified / candidate |
| towns.013 | Town food stock | production; consumption; siege; starvation | stock, food change and starvation casualties agree | unverified / candidate |
| towns.014 | Town rebellion | eligibility; outbreak; new clan; ownership | new rebel objects are registered and state changes reach both clients | unverified / candidate |
| towns.015 | Town market tax | trade; daily accumulation; owner payout | trade tax and payout occur for the correct owner | unverified / candidate |
| towns.016 | Settlement capture | battle; simulation; kingdom allocation | new ownership and garrison/dungeon side effects agree | unverified / candidate |
| towns.017 | Settlement devastation | pillage; mercy; devastation choice | accepted aftermath choice yields the intended state and relation changes | unverified / candidate |

Sources: [source/GameInterface/Services/Fiefs](../source/GameInterface/Services/Fiefs), [source/GameInterface/Services/Settlements](../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Towns](../source/GameInterface/Services/Towns)

## villages

Entry: Village menu / campaign map

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| villages.001 | Enter village | normal; raided; hostile; own village | menu reflects authoritative village state and access | unverified / candidate |
| villages.002 | Recruit volunteers | available slots; relation restriction; forced recruitment | volunteer stock and owning player roster balance | unverified / candidate |
| villages.003 | Village trade | buy; sell; food; ordinary goods | market stock and player resources balance | unverified / candidate |
| villages.004 | Raid village | start; progress; interruption; completion | raid state and loot advance under authoritative campaign ticks | unverified / candidate |
| villages.005 | Force recruits | accepted hostile action; combat; cancellation | recruits and relation/crime effects occur only for the completed action | unverified / candidate |
| villages.006 | Take supplies | accepted hostile action; combat; cancellation | received goods and village consequences converge | unverified / candidate |
| villages.007 | Village production | production type; daily stock; bound town | produced goods and shipment state agree | unverified / candidate |
| villages.008 | Village hearths | growth; raid damage; healing | hearth count and dependent production agree | unverified / candidate |
| villages.009 | Village recovery | raided; recovering; normal | state and restored access follow authoritative healing ticks | unverified / candidate |
| villages.010 | Village militia | spawn; battle loss; daily growth | militia identity and roster converge | unverified / candidate |
| villages.011 | Villager party departure | goods ready; town route; party creation | one registered villager party carries the intended goods | unverified / candidate |
| villages.012 | Villager party return | successful sale; intercepted; target changed | village stock and party lifecycle reach the intended state | unverified / candidate |

Sources: [source/GameInterface/Services/Actions](../source/GameInterface/Services/Actions), [source/GameInterface/Services/VillageMarketDatas](../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Villages](../source/GameInterface/Services/Villages)

## buildings

Entry: Town/castle construction interface

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| buildings.001 | Select construction project | available building; unavailable project | selected queue entry names the intended building | unverified / candidate |
| buildings.002 | Change construction queue | append; reorder; remove | queue order matches the accepted client action | unverified / candidate |
| buildings.003 | Change daily default project | housing; irrigation; militia; festival where available | selected daily project and observed effect match the installed options | unverified / candidate |
| buildings.004 | Fund construction | deposit; withdraw; insufficient gold | reserve and player gold balance | unverified / candidate |
| buildings.005 | Construction progress | daily tick; boosted; paused by low loyalty | progress agrees across peers and completion occurs once | unverified / candidate |
| buildings.006 | Building completion | level upgrade; queue advances | new building level and next active project converge | unverified / candidate |
| buildings.007 | Building effects | food; security; prosperity; militia; siege defenses | measure the selected building's independent expected settlement effect | unverified / candidate |

Sources: [source/GameInterface/Services/Buildings](../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../source/GameInterface/Services/Towns)

## locations

Entry: Town/castle/village scene / conversation

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| locations.001 | Walk town center | spawn; move; interact; leave | client enters the intended town scene with correct actors | unverified / candidate |
| locations.002 | Enter lord's hall | permission; bribe; disguise; denied | scene access and authoritative consequences match accepted action | unverified / candidate |
| locations.003 | Enter tavern | town menu; scene transition; return | correct tavern NPCs and player identities appear | unverified / candidate |
| locations.004 | Enter prison | permission; ransom; prison break context | correct prisoner characters are presented in the intended prison | unverified / candidate |
| locations.005 | Walk village | normal village; hostility; scene return | village scene and campaign return agree | unverified / candidate |
| locations.006 | Talk to notable | issue giver; recruitment; caravan | conversation uses the selected registered hero | unverified / candidate |
| locations.007 | Talk to companion | hire; dismiss; party assignment | dialogue outcome affects only the intended player relationship | unverified / candidate |
| locations.008 | Location character creation | new character; visitor; late entrant | the same registered character appears once in the location | unverified / candidate |
| locations.009 | Location character removal | leave; death; party departed | removed actor does not remain as a divergent scene copy | unverified / candidate |
| locations.010 | Special location items | add; remove; duplicate; reload | location's item set agrees across peers without duplicates | unverified / candidate |
| locations.011 | Scene transition | tavern to center; prison to center; campaign map | owned mission exits and next scene retains the intended campaign context | unverified / candidate |
| locations.012 | Barber appearance change | available actor; payment; cancel | accepted appearance changes reach both clients | unverified / candidate |
| locations.013 | Board game interaction | start; move; win/loss; leave | game state and reward agree where the current co-op path exists | unverified / candidate |

Sources: [source/GameInterface/Services/Characters](../source/GameInterface/Services/Characters), [source/GameInterface/Services/Locations](../source/GameInterface/Services/Locations), [source/GameInterface/Services/Missions](../source/GameInterface/Services/Missions)

## alleys

Entry: Town common area / alley interaction

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| alleys.001 | Challenge alley gang | enter area; provoke; combat | alley encounter participants and consequences match the selected area | unverified / candidate |
| alleys.002 | Take over alley | win fight; cancel; companion assignment | ownership and installed player-alley data agree | unverified / candidate |
| alleys.003 | Assign alley leader | eligible companion; replacement; invalid hero | assigned leader is the accepted eligible hero | unverified / candidate |
| alleys.004 | Transfer alley troops | deposit; withdraw; capacity | party and alley troop rosters balance | unverified / candidate |
| alleys.005 | Alley income | daily tick; criminal rating; owner | income reaches the intended player and area state | unverified / candidate |
| alleys.006 | Lose alley | rival takeover; abandonment; owner unavailable | ownership, troops and leader assignment reach the intended state | unverified / candidate |

Sources: [source/GameInterface/Services/Alleys](../source/GameInterface/Services/Alleys), [source/GameInterface/Services/Crime](../source/GameInterface/Services/Crime)

## prisoners

Entry: Party/dungeon screen / captivity / prison break

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| prisoners.001 | Capture hero | battle; surrender; authoritative action | hero prisoner state and captor roster agree | unverified / candidate |
| prisoners.002 | Release hero prisoner | free; barter; ransom; peace | prisoner leaves the captor once and state/location update | unverified / candidate |
| prisoners.003 | Offer ransom | new offer; accepted; rejected; expired | gold and release occur only for accepted valid terms | unverified / candidate |
| prisoners.004 | Hero escapes captivity | party prisoner; settlement prisoner; AI tick | released hero and subsequent party/location state converge | unverified / candidate |
| prisoners.005 | Player captured | battle defeat; surrender | the intended player enters captivity without losing ownership mapping | unverified / candidate |
| prisoners.006 | Player captivity progress | wait; transfer captor; escape opportunity | captivity UI and authoritative state remain consistent | unverified / candidate |
| prisoners.007 | Player released from captivity | ransom; escape; captor defeated | controlled party recovery produces one registered valid party | unverified / candidate |
| prisoners.008 | Prison break | start; guards; rescue; success; failure | prisoners and mission/campaign consequences reflect the real outcome | unverified / candidate |
| prisoners.009 | Prisoner conformity | daily tick; troop type; recruit eligibility | conformity and recruitable count agree | unverified / candidate |
| prisoners.010 | Prisoner limit | within cap; exceeded; escape | UI and resulting prisoner losses follow the current model | unverified / candidate |

Sources: [source/GameInterface/Services/Actions](../source/GameInterface/Services/Actions), [source/GameInterface/Services/Heroes](../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Party](../source/GameInterface/Services/Party), [source/GameInterface/Services/PlayerCaptivityService](../source/GameInterface/Services/PlayerCaptivityService)

## clans

Entry: Clan screen / lord conversation

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| clans.001 | Create clan | new player; initial name/banner | one clan is linked to the intended player hero | unverified / candidate |
| clans.002 | Inspect clan roster | family; companions; leaders; parties | UI represents authoritative membership and assignments | unverified / candidate |
| clans.003 | Clan tier increase | renown threshold; multiple players | tier and unlocked limits belong to the correct clan | unverified / candidate |
| clans.004 | Gain renown | battle; tournament; quest | reward reaches the player/clan that earned it | unverified / candidate |
| clans.005 | Gain or spend influence | battle; army; vote; proposal | influence changes once for the accepted action | unverified / candidate |
| clans.006 | Change clan leader | death; retirement; explicit selection | membership and leader-dependent effects converge | unverified / candidate |
| clans.007 | Join kingdom as vassal | oath; invitation; eligible/ineligible | clan kingdom changes only for accepted valid entry | unverified / candidate |
| clans.008 | Sign mercenary contract | join; renew; leave | contract state and payment match the correct clan | unverified / candidate |
| clans.009 | Leave kingdom | keep fiefs; relinquish; confirmation cancelled | clan/fief/diplomatic consequences reflect the accepted choice | unverified / candidate |
| clans.010 | Clan finances | party wages; workshop; caravan; tribute | daily accounting belongs to the correct clan and is applied once | unverified / candidate |
| clans.011 | Clan destruction | last member; loss conditions | dependent ownership and kingdom membership are updated consistently | unverified / candidate |

Sources: [source/GameInterface/Services/Clans](../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../source/GameInterface/Services/Fiefs)

## kingdoms

Entry: Kingdom screen / diplomacy / decision UI

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| kingdoms.001 | Create kingdom | eligibility; governor conversation; naming | one registered kingdom is created with intended leader and clan | unverified / candidate |
| kingdoms.002 | Dissolve kingdom | valid conditions; remaining clans/fiefs | membership and stance links reach the correct final state | unverified / candidate |
| kingdoms.003 | Declare war | proposal; decision; direct action | all affected factions share the authoritative war stance | unverified / candidate |
| kingdoms.004 | Make peace | proposal; decision; tribute terms | peace state and accepted payments converge | unverified / candidate |
| kingdoms.005 | Form alliance | proposal; accept; reject; existing alliance | agreement state changes once under current eligibility rules | unverified / candidate |
| kingdoms.006 | End alliance | expiry; war; explicit action | stance/agreement state and downstream UI agree | unverified / candidate |
| kingdoms.007 | Create trade agreement | proposal; acceptance; expiration | agreement and economic consequences reach affected factions | unverified / candidate |
| kingdoms.008 | Call ally to war | propose; ally decision; accept/reject | war entry follows only an accepted current call | unverified / candidate |
| kingdoms.009 | Pay tribute | daily tick; changed terms; peace ends | payer and payee accounting reflects the active agreement | unverified / candidate |
| kingdoms.010 | Propose kingdom policy | new; already active; insufficient influence | a valid current decision is registered or rejected with reason | unverified / candidate |
| kingdoms.011 | Revoke kingdom policy | active policy; invalid policy | accepted outcome removes the correct policy | unverified / candidate |
| kingdoms.012 | Allocate settlement | preliminary claimant; final claimant; owner changed | accepted decision assigns the intended eligible clan | unverified / candidate |
| kingdoms.013 | Elect ruler | leader death; candidates; outcome | one eligible leader becomes authoritative | unverified / candidate |
| kingdoms.014 | Expel clan | proposal; influence; voting | accepted expulsion changes clan membership and side effects | unverified / candidate |
| kingdoms.015 | Submit decision vote | support; oppose; abstain; influence strength | vote belongs to the player's clan and current decision round | unverified / candidate |
| kingdoms.016 | Update decision vote | change strength; repeated click; late vote | one current vote replaces the prior vote under round rules | unverified / candidate |
| kingdoms.017 | Decision deadline | before; at; after expiration | round closes once and late requests do not reopen it | unverified / candidate |
| kingdoms.018 | Resolve decision | accepted outcome; rejected outcome; removed decision | outcome is applied once and pending UI clears | unverified / candidate |
| kingdoms.019 | Decision presentation | waiting; can vote; resolved | client UI agrees with the same round/session identity | unverified / candidate |
| kingdoms.020 | Concurrent proposals | same subject; conflicting war/peace; decision removed | only valid current decisions reach an authoritative outcome | unverified / candidate |

Sources: [source/Coop.Core/Server/Services/Kingdoms](../source/Coop.Core/Server/Services/Kingdoms), [source/GameInterface/Services/Alliances](../source/GameInterface/Services/Alliances), [source/GameInterface/Services/Kingdoms](../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/StanceLinks](../source/GameInterface/Services/StanceLinks)

## armies

Entry: Campaign army UI / map

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| armies.001 | Create army | leader eligible; influence; available parties | one registered army owns the intended leader and invited parties | unverified / candidate |
| armies.002 | Invite party to army | eligible; already attached; other player | accepted party joins the intended army once | unverified / candidate |
| armies.003 | Join army | client request; automatic AI arrival | party attachment and army membership agree | unverified / candidate |
| armies.004 | Leave army | voluntary; battle; disband | party detaches from the intended army and can move independently | unverified / candidate |
| armies.005 | Army cohesion | daily decay; spend influence; leader change | cohesion/influence change once on the authoritative side | unverified / candidate |
| armies.006 | Army supplies | food sharing; shortages; party loss | food and shortage effects agree for participating parties | unverified / candidate |
| armies.007 | Army objective | attack; defend; besiege; patrol | member movement follows the current authoritative army order | unverified / candidate |
| armies.008 | Army encounter | field battle; siege defense; relief | all participating parties share the correct map event sides | unverified / candidate |
| armies.009 | Disband army | leader action; cohesion loss; leader removed | all members detach without retaining a dead army reference | unverified / candidate |
| armies.010 | Army leader replacement | leader removed; eligible survivor | new leader and resulting orders agree | unverified / candidate |

Sources: [source/GameInterface/Services/Armies](../source/GameInterface/Services/Armies), [source/GameInterface/Services/MapEvents](../source/GameInterface/Services/MapEvents), [source/GameInterface/Services/MobileParties](../source/GameInterface/Services/MobileParties)

## quests

Entry: Notable/lord conversation / quest journal

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| quests.001 | Discover issue | eligible giver; normal generation; disabled type | only source-allowlisted issues appear through normal supported admission | unverified / source-inspected |
| quests.002 | View issue dialogue | issue icon; task entry; alternate task entry | dialogue entry follows the exact allowlist gate | unverified / source-inspected |
| quests.003 | Accept issue personally | eligible; already active; ownership conflict | accepted quest belongs to the intended player | unverified / source-inspected |
| quests.004 | Accept alternative solution | eligible companion; troop selection; cancellation | assigned companion/troops and ownership state agree | unverified / source-inspected |
| quests.005 | Advance quest progress | production action; unrelated player action | only relevant owned progress updates the quest | unverified / source-inspected |
| quests.006 | Quest time limit | before deadline; expiration; offline owner | expiration and outcome occur once for the correct quest | unverified / source-inspected |
| quests.007 | Quest success | valid production completion; partial objective | reward and finalization reach the owning player once | unverified / source-inspected |
| quests.008 | Quest failure | objective failure; giver unavailable; rival state | failure consequences and quest removal converge | unverified / source-inspected |
| quests.009 | Quest betrayal or cancellation | dialogue choice; invalid terminal request | accepted terminal state is applied once with correct consequences | unverified / source-inspected |
| quests.010 | Quest journal | new log; progress; completed log | attempt to enter vanilla quest journal is blocked by the current QuestsState gate; private journal/progress support remains unverified | disabled by QuestsState gate / source-inspected |
| quests.011 | Alternative solution return | duration; troop loss; companion recovery | returned roster, companion and rewards reach the owner | unverified / source-inspected |
| quests.012 | Quest persistence | save/reload; reconnect; alternative in progress | ownership and progress resume without replayed rewards | unverified / source-inspected |
| quests.013 | Debug issue grant catalog | listed; wired; unwired; normal gate disabled | debug grant availability is reported separately from normal quest support | unverified / source-inspected |

Sources: [features/baseline/installed.json](../features/baseline/installed.json), [source/GameInterface/Services/Issues](../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [source/GameInterface/Services/UI/Patches/GameUIDisable.cs](../source/GameInterface/Services/UI/Patches/GameUIDisable.cs)

## tournaments

Entry: Town arena / co-op tournament lobby

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| tournaments.001 | Generate tournament | town availability; existing tournament | one town tournament becomes available without duplicate creation | unverified / source-inspected |
| tournaments.002 | Join tournament lobby | first client; second client; repeated join | both clients reference one server session with distinct participants | unverified / source-inspected |
| tournaments.003 | Start tournament | controller; noncontroller; lobby not ready | only a valid current start advances the session once | unverified / source-inspected |
| tournaments.004 | Choose participate | current match; stale match; repeated choice | current participant choice is accepted once | unverified / source-inspected |
| tournaments.005 | Choose spectate | eligible observer; match underway | client enters the observer path for the current match | unverified / source-inspected |
| tournaments.006 | Tournament betting | valid amount; insufficient funds; repeated bet | accepted bet consumes funds once and uses the current round | unverified / source-inspected |
| tournaments.007 | Tournament match combat | team identity; mount; casualties; victory | both clients observe the same round participants and outcome | unverified / source-inspected |
| tournaments.008 | Tournament progression | round win; loss; final round | bracket and phase advance once from the authoritative outcome | unverified / source-inspected |
| tournaments.009 | Tournament reward | prize item; gold; renown; winner identity | reward reaches the actual winning player's party/clan | unverified / source-inspected |
| tournaments.010 | Leave tournament | preparation; match; completed; disconnect | session membership and remaining client state remain consistent | unverified / source-inspected |
| tournaments.011 | Tournament spectator disconnect | pacer/observer departure; remaining players | remaining players do not become stranded in a completed/stale mission | unverified / source-inspected |
| tournaments.012 | Tournament fixture restoration | normal completion; abort; failed setup | owned fixture restores its captured baseline only after the session ends | unverified / source-inspected |

Sources: [source/E2E.Tests](../source/E2E.Tests), [source/GameInterface/Services/Tournaments](../source/GameInterface/Services/Tournaments), [source/Missions](../source/Missions)

## battles

Entry: Campaign encounter / rendered battle mission

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| battles.001 | Attack enemy party | first encounter; two clients; AI ally | correct registered parties enter the same authoritative map event | unverified / candidate |
| battles.002 | Join allied battle | attacker; defender; reinforcement; late entry | joining party and player appear on the intended battle side | unverified / candidate |
| battles.003 | Simulate battle | attack; defense; participating player parties | resulting losses/prisoners/rewards agree without rendered combat | unverified / candidate |
| battles.004 | Battle preparation | deployment; equipment; side assignment | player and troops spawn on the correct battle team | unverified / candidate |
| battles.005 | Spawn troops | initial wave; reinforcements; wounded excluded | spawned/reserve counts conserve the authoritative roster | unverified / candidate |
| battles.006 | Spawn player hero | owner; remote peer; reconnect | one active agent represents each participating player hero | unverified / candidate |
| battles.007 | Agent movement | walk; run; jump; crouch; rotate | remote agent position/animation follows the owned agent | unverified / candidate |
| battles.008 | Weapon attack | swing directions; thrust; weapon mode | remote clients observe the same accepted attack and resulting hit | unverified / candidate |
| battles.009 | Block attack | directional weapon block; shield block | block/damage outcome agrees across the battle | unverified / candidate |
| battles.010 | Kick or shield bash | hit; miss; interrupted | impulse/stun and damage outcomes converge | unverified / candidate |
| battles.011 | Ranged attack | bow; crossbow; throwing; projectile miss/hit | projectile and resulting damage are applied once | unverified / candidate |
| battles.012 | Switch equipment | weapon slot; pickup; drop; depleted ammunition | equipment identity and ammo state agree | unverified / candidate |
| battles.013 | Mount or dismount | owned mount; shared mount; dead mount | rider/mount ownership and attachment match on both clients | unverified / candidate |
| battles.014 | Horse damage | charge; projectile; death; rider fall | health and mounted state converge with authoritative casualty reporting | unverified / candidate |
| battles.015 | Troop health | damage; wound; kill; collection during battle | each health update belongs to the correct troop/reserve entry | unverified / candidate |
| battles.016 | Player health | damage; incapacitation; healing if permitted | owning and observing clients agree on health/outcome | unverified / candidate |
| battles.017 | Formation captain | player; AI hero; leader lost | formation authority and bonuses follow the eligible captain | unverified / candidate |
| battles.018 | Formation selection | infantry; ranged; cavalry; multiple groups | only selected owned formations receive the order | unverified / candidate |
| battles.019 | Formation movement order | move; follow; charge; advance; retreat; hold | troops follow the current valid order on the correct team | unverified / candidate |
| battles.020 | Formation arrangement | line; shield wall; loose; square; circle; column | actual eligible formation arrangement matches the selected order | unverified / candidate |
| battles.021 | Formation firing order | hold fire; fire at will | ranged troops obey the accepted order | unverified / candidate |
| battles.022 | Formation mount order | mount; dismount; unavailable mounts | eligible troops change mounted state consistently | unverified / candidate |
| battles.023 | Reinforcement wave | initial supply exhausted; reserve remains | new agents consume the correct reserve entries once | unverified / candidate |
| battles.024 | Battle retreat | player retreat; troop rout; wounded survivor | casualty accounting and returned rosters conserve troop counts | unverified / candidate |
| battles.025 | Battle authority handoff | pacer leaves; reconnect; pending spawn | remaining peers retain one current authority and valid battle state | unverified / candidate |
| battles.026 | Battle completion | victory; defeat; draw; all participants removed | map event finalizes once with consistent surviving rosters | unverified / candidate |
| battles.027 | Battle loot | winner; participant contribution; abandoned loot | accepted loot reaches intended player inventories | unverified / candidate |
| battles.028 | Battle prisoners | hero capture; troop capture; escaped hero | prisoner identities and rosters agree | unverified / candidate |
| battles.029 | Post-battle relations/rewards | renown; influence; skill XP; quest progress | each reward/side effect targets its actual eligible owner | unverified / candidate |
| battles.030 | Mission exit | normal; retreat; crash; completed battle | client returns to the correct campaign state with no retained live mission | unverified / candidate |

Sources: [source/GameInterface/Services/MapEvents](../source/GameInterface/Services/MapEvents), [source/GameInterface/Services/Missions](../source/GameInterface/Services/Missions), [source/Missions/Battles](../source/Missions/Battles)

## sieges

Entry: Siege camp / settlement siege menu / assault mission

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| sieges.001 | Begin siege | town; castle; army leader; lone party | one registered siege event links correct besieger and settlement | unverified / candidate |
| sieges.002 | Join besieger camp | leader; army member; second player | party joins the correct siege side and observes shared progress | unverified / candidate |
| sieges.003 | Leave siege | leader departure; nonleader departure; final besieger | siege persists or ends according to remaining authoritative participants | unverified / candidate |
| sieges.004 | Build siege camp | progress; interruption; settlement relieved | camp readiness agrees across peers | unverified / candidate |
| sieges.005 | Queue siege engine | ram; tower; catapult; ballista; trebuchet where available | selected valid engine enters the correct construction slot | unverified / candidate |
| sieges.006 | Construct siege engine | progress; completion; destroyed during build | engine identity and progress agree | unverified / candidate |
| sieges.007 | Move engine to reserve | active slot; reserved slot; replacement | one engine occupies the intended slot/reserve | unverified / candidate |
| sieges.008 | Siege bombardment | engine hit; wall damage; engine destroyed | health and construction/destruction effects converge | unverified / candidate |
| sieges.009 | Wall breach | unbreached; one breach; multiple breaches | mission defenses reflect authoritative wall state | unverified / candidate |
| sieges.010 | Siege supply shortage | food exhausted; garrison starvation; militia | food and troop attrition agree without double daily losses | unverified / candidate |
| sieges.011 | Lead siege assault | ready; not ready; attacking army | all participating clients enter the correct assault map event | unverified / candidate |
| sieges.012 | Defend siege | garrison; militia; player party; relief army | defender roster and battle side contain the intended participants | unverified / candidate |
| sieges.013 | Sally out | eligible defenders; blocked action | accepted sally creates the correct encounter and returns state | unverified / candidate |
| sieges.014 | Siege ambush | launch; destroy engines; retreat; completion | engine losses and returned party state agree | unverified / candidate |
| sieges.015 | Relieve siege | attack besiegers; join defense; besieger defeat | siege lifecycle and army attachments converge | unverified / candidate |
| sieges.016 | Capture settlement aftermath | show mercy; pillage; devastate | accepted choice produces the intended ownership/roster/relations state | unverified / candidate |

Sources: [source/GameInterface/Services/BesiegerCamps](../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../source/GameInterface/Services/MapEvents), [source/GameInterface/Services/SiegeEngines](../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/SiegeEvents](../source/GameInterface/Services/SiegeEvents), [source/Missions](../source/Missions)

## naval

Entry: Installed naval menu paths / optional mod mission module

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| naval.001 | Embark party | port; shoreline where allowed; ships available | embark path exists for the enabled module set and retains party identity | unknown; no Missions.Naval project in bound tree / candidate |
| naval.002 | Disembark party | port; landing; invalid location | accepted landing produces one authoritative land party | unknown; no Missions.Naval project in bound tree / candidate |
| naval.003 | Naval campaign movement | sea route; destination; retreat | party movement and ship identity converge where naval opt-in is active | unknown; no Missions.Naval project in bound tree / candidate |
| naval.004 | Naval encounter | hostile fleet; join ally; disengage | current encounter side and outcome agree for enabled naval support | unknown; no Missions.Naval project in bound tree / candidate |
| naval.005 | Naval battle entry | ship selection; player/team ownership | players board the intended registered vessels | unknown; no Missions.Naval project in bound tree / candidate |
| naval.006 | Ship movement | course; speed; collision | ship authority and observed movement agree | unknown; no Missions.Naval project in bound tree / candidate |
| naval.007 | Boarding combat | board; repel; casualties | agents and roster casualties remain associated with correct parties | unknown; no Missions.Naval project in bound tree / candidate |
| naval.008 | Naval projectile hit | ship; crew; water miss | impact/damage is applied once to the intended target | unknown; no Missions.Naval project in bound tree / candidate |
| naval.009 | Naval disable/opt-in boundary | module absent; opt-in absent; enabled | unsupported launch/action is visible and not reported as tested support | unknown; no Missions.Naval project in bound tree / candidate |

Sources: [features/baseline/installed.json](../features/baseline/installed.json), [source/GameInterface/Services/MobileParties](../source/GameInterface/Services/MobileParties), [source/Missions](../source/Missions)

## ai

Entry: Authoritative campaign ticks / observed map state

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| ai.001 | AI strategic objective | attack; defend; recruit; resupply | authoritative goal/behavior is replicated to observing clients | unverified / candidate |
| ai.002 | AI party think | daily/hourly tick; player nearby | client-disabled behavior does not create a second decision | unverified / candidate |
| ai.003 | AI army movement | member; leader; disband | parties follow the same authoritative army objective | unverified / candidate |
| ai.004 | AI recruitment | stock; available gold; roster capacity | volunteers/resources and roster changes occur once | unverified / candidate |
| ai.005 | AI food purchase | market visit; insufficient stock | party/market resources balance after the authoritative purchase | unverified / candidate |
| ai.006 | AI prisoner sale | settlement visit; captives | gold and prisoner roster changes occur once | unverified / candidate |
| ai.007 | AI loot sale | market visit; sale inventory | party trade funds and stock agree | unverified / candidate |
| ai.008 | Bandit spawning | spawn eligibility; cap; party creation | one registered bandit party appears per authoritative spawn | unverified / candidate |
| ai.009 | Hideout spawning | location available; cap; removal | hideout and resident parties have consistent identities | unverified / candidate |
| ai.010 | Desertion | low morale; unpaid wages; over capacity | deserting troops and any created party are consistent | unverified / candidate |
| ai.011 | Rebellion AI | new faction; nearby parties; defeat | new objects are registered and remaining world state converges | unverified / candidate |

Sources: [source/GameInterface/Services/Actions](../source/GameInterface/Services/Actions), [source/GameInterface/Services/Bandits](../source/GameInterface/Services/Bandits), [source/GameInterface/Services/MobilePartyAIs](../source/GameInterface/Services/MobilePartyAIs)

## presentation

Entry: Client campaign HUD / encyclopedia / map / notifications

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| presentation.001 | Party map banner | owner; banner edit; kingdom change | visual banner matches authoritative ownership/design | unverified / candidate |
| presentation.002 | Party map name | hero/clan rename; hidden party | visible name matches the permitted current party state | unverified / candidate |
| presentation.003 | Party tooltip | troop count; prisoners; morale; speed | displayed values agree with independently observed state | unverified / candidate |
| presentation.004 | Settlement tooltip | owner; garrison; militia; siege; stocks | displayed settlement state agrees across clients | unverified / candidate |
| presentation.005 | Campaign notifications | quest; kingdom; hero; battle | notification references the correct current event and recipient | unverified / candidate |
| presentation.006 | Encyclopedia hero page | location; clan; relation; biography | page uses the intended current registered hero | unverified / candidate |
| presentation.007 | Encyclopedia clan page | members; leader; kingdom; fiefs | page matches authoritative clan state | unverified / candidate |
| presentation.008 | Encyclopedia kingdom page | clans; war/peace; leader | page matches authoritative kingdom state | unverified / candidate |
| presentation.009 | Encyclopedia settlement page | owner; bound villages; economy | page refers to the actual settlement and current owner | unverified / candidate |
| presentation.010 | Player list | join; leave; identity; party | list reflects current admitted clients without duplicate identities | unverified / candidate |
| presentation.011 | Text chat | send; receive; channel; reconnect | intended connected peers see one copy with correct sender | unverified / candidate |
| presentation.012 | Voice communication | transmit; receive; muted; disconnected | audio routing and mute state follow the selected peers | unverified / candidate |
| presentation.013 | Loading screen | save transfer; campaign load; failure | loading presentation corresponds to actual readiness/failure | unverified / candidate |
| presentation.014 | Map event marker | battle; siege; event completion | marker appears and disappears with the correct authoritative event | unverified / candidate |
| presentation.015 | Narrative incident UI | ordinary incident; authoritative world continues | InvokeIncident is suppressed by the current patch; do not report this UI as verified gameplay | disabled by InvokeIncident prefix / source-inspected |

Sources: [UIMovies](../UIMovies), [source/GameInterface/Services/Chat](../source/GameInterface/Services/Chat), [source/GameInterface/Services/PartyVisuals](../source/GameInterface/Services/PartyVisuals), [source/GameInterface/Services/UI](../source/GameInterface/Services/UI), [source/GameInterface/Services/UI/Patches/IncidentDisable.cs](../source/GameInterface/Services/UI/Patches/IncidentDisable.cs), [source/GameInterface/Services/Voice](../source/GameInterface/Services/Voice)

## options

Entry: Client options / co-op options / time controls

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| options.001 | Co-op options menu | open; edit; confirm; cancel | accepted options persist and affect only the intended scope | unverified / candidate |
| options.002 | Campaign pause | menu pause; player request; policy restrictions | effective time control follows authoritative pause policy | unverified / candidate |
| options.003 | Campaign time speed | normal; fast; accelerated; conflicting requests | all peers use the effective authoritative time rate | unverified / candidate |
| options.004 | Time while players disconnected | none; one remaining; host idle | configured server policy controls campaign progression | unverified / candidate |
| options.005 | Difficulty settings | damage; recruitment; combat; campaign parameters | current effective option matches persisted configuration | unverified / candidate |
| options.006 | Audio/graphics/input options | apply; cancel; restart-dependent | local preference affects intended rendering/input without gameplay state divergence | unverified / candidate |
| options.007 | Hotkey interaction | map; inventory; character; mission | input reaches the visible intended context | unverified / candidate |

Sources: [source/GameInterface/Services/Difficulties](../source/GameInterface/Services/Difficulties), [source/GameInterface/Services/Time](../source/GameInterface/Services/Time), [source/GameInterface/Services/UI/CoopOptions](../source/GameInterface/Services/UI/CoopOptions)

## save

Entry: Server save / session sidecar / startup save selection

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| save.001 | Save campaign | manual; automatic; pending authoritative action | completed save is readable and identified after write completion | unverified / candidate |
| save.002 | Save player registrations | multiple clients; disconnected player | session metadata retains correct platform/hero/party mappings | unverified / candidate |
| save.003 | Save quest ownership | personal; alternative in progress; completed | owner/progress survive without reward replay | unverified / candidate |
| save.004 | Save crafted items | new weapon; custom name; design identity | item and design reload with stable identity | unverified / candidate |
| save.005 | Save dynamic world objects | new party; rebel clan; newborn hero | reloaded objects register once and references resolve | unverified / candidate |
| save.006 | Load existing campaign | compatible; missing; corrupt | loaded identity/readiness is observed or concrete load error is retained | unverified / candidate |
| save.007 | Select startup save | exact catalog basename; missing; unreadable | requested selection is distinct from actual confirmed campaign identity | unverified / candidate |
| save.008 | Join after reload | returning player; new player | correct saved or new hero/party is admitted once | unverified / candidate |
| save.009 | Save shutdown continuity | save active; process exit; cancellation | only verified completed saves are treated as durable recovery points | unverified / candidate |
| save.010 | Session sidecar missing | campaign save exists; no player metadata | missing registration persistence is reported without invented recovery | unverified / candidate |
| save.011 | Client escape-menu save buttons | Save; Save As; Save And Exit; refresh | client save actions remain disabled after refresh and identify server-owned campaign saving | disabled on clients / source-inspected |

Sources: [doc/automated-testing/mcp-deployment.md](../doc/automated-testing/mcp-deployment.md), [source/Coop.Core](../source/Coop.Core), [source/GameInterface/Services/Save](../source/GameInterface/Services/Save), [source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs](../source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs), [tools/CoopMcpServer](../tools/CoopMcpServer)

## tools

Entry: Repository selector / direct bannerlord-coop MCP / evidence

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| tools.001 | Select required verification | known documentation; unknown path; invalid identity | unit selection, fail-closed full-live selection and invalid-input exit are independently asserted | tooling exercised; no gameplay proof / exercised |
| tools.002 | Read launch preflight | commit headroom; bridge compatibility; ownership | read-only preflight reports prerequisites without launching/deploying | tooling-only / source-inspected |
| tools.003 | Read runtime readiness | started; readyToJoin; readyForCampaignTests | launch and process liveness are distinguished from campaign readiness | tooling-only / source-inspected |
| tools.004 | Drive registered command | side allowed; arguments valid; framework failure | structured success/failure and actual world outcome agree | tooling-only / source-inspected |
| tools.005 | Inspect native UI | layers; current snapshot; stale target | interaction targets the current visible eligible widget | tooling-only / source-inspected |
| tools.006 | Capture rendered screenshot | correct client; frame completed; failed capture | evidence comes from the intended rendered client and retained frame | tooling-only / source-inspected |
| tools.007 | Read incremental logs | cursor; rotation; stopped archive | log evidence remains attributed to the correct instance/run | tooling-only / source-inspected |
| tools.008 | Stop owned runtime | partial launch; normal; failed cleanup | owned process cleanup is confirmed and unrelated processes survive | tooling-only / source-inspected |
| tools.009 | Deploy selected Debug build | explicit deployment; immutable manifest; rollback | installed hashes/MVIDs match tested source or a concrete recovery blocker remains | tooling-only / source-inspected |
| tools.010 | Retain evidence | success; failure; uncertain mutation | raw artifacts remain readable after cleanup with exact source/run identity | tooling-only / source-inspected |

Sources: [doc/automated-testing/verification-harness.md](../doc/automated-testing/verification-harness.md), [source/VerificationHarness](../source/VerificationHarness), [tools/CoopMcpServer](../tools/CoopMcpServer)

## vanilla-modes

Entry: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points

| ID | Action | Outcome variants | Required observation | Support / evidence |
| --- | --- | --- | --- | --- |
| vanilla-modes.001 | Sandbox campaign | new game; saved game | entry and behavior registration are established for the exact installed build | unverified / candidate |
| vanilla-modes.002 | Story campaign | tutorial; story progression; quest phases | StoryMode-owned paths require separate source and runtime evidence | unverified / candidate |
| vanilla-modes.003 | Custom battle | battle setup; teams; troop selection | standalone mode is kept separate from co-op campaign claims | unverified / candidate |
| vanilla-modes.004 | Native multiplayer | lobby; matchmaking; multiplayer modes | native multiplayer behavior is not inferred from co-op campaign networking | unverified / candidate |
| vanilla-modes.005 | DLC module availability | installed; selected; licensed; opt-in | module availability and active gameplay support are reported separately | unverified / candidate |
| vanilla-modes.006 | Launcher and module selection | required baseline modules; Coop declaration | selected module set matches the bound installed/deployed source identity | unverified / candidate |

Sources: [features/baseline/installed.json](../features/baseline/installed.json), [source/Coop/CoopMod.cs](../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../source/GameInterface/Services/UI)
