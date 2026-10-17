# Inspecting affected perk behavior

Perk selection and each primary/secondary effect are separate acceptance obligations when
a change affects them, including when both declared roles match. The compact map groups
player-visible progression and role behavior rather than committing a page for every definition.

```powershell
python tools/feature_map.py find WrappedHandles --kind vanilla-perk
python tools/feature_map.py find progression --kind feature
```

[The installed snapshot](baseline/installed.json) retains 374 choices, their alternative,
skill threshold expression, declared roles, raw bonuses and increments. These are definitions
from the named Native v1.4.8 DLL, not observed selection or effect application.

Before writing an effect oracle, inspect the exact installed `DefaultPerks`/`PerkObject`
definition and complete consumer/caller. Trace which effect argument is used, eligible hero
role, troop mask, context, cap, formula and any co-op model replacement. A located perk name
does not automatically identify the primary or secondary apply path. Raw `AddFactor` values
are not measured percentages; zero bonus does not mean no capability. Do not invent a
secondary effect when the definition has none.

Retain the relevant spans/build identities and compare holder versus nonholder or ineligible
role/troop/context through real actions. Observe each required owner/shared consequence;
persistence of selection alone does not prove an effect. New effects need their own source
case and runtime evidence under [the evidence contract](granularity.md).
