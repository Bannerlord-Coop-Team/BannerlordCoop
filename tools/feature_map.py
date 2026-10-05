"""Read, refresh and validate the Bannerlord feature map. Never builds or launches a game."""

import argparse
import csv
import hashlib
import html
import io
import json
import re
import subprocess
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FEATURES = ROOT / "features"
INVENTORY = FEATURES / "inventory.csv"
COLUMNS = ("kind", "domain", "id", "entry", "side", "arguments", "source", "line", "sha256", "status")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(path):
    return path.relative_to(ROOT).as_posix()


def source_files():
    # Git excludes packages, outputs, local installations and unrelated worktrees.
    result = subprocess.run(
        ["git", "ls-files", "source", "tools/CoopMcpServer"], cwd=ROOT,
        check=True, capture_output=True, text=True,
    )
    return [ROOT / name for name in result.stdout.splitlines() if name.endswith(".cs")]


def domain(path):
    parts = path.parts
    if "Services" in parts:
        index = parts.index("Services") + 1
        if index < len(parts):
            return parts[index]
    return parts[1] if len(parts) > 1 else "Runtime"


def inventory_rows():
    rows = []

    def add(kind, area, key, entry, side, arguments, source, line, sha, status):
        rows.append(dict(zip(COLUMNS, (kind, area, key, entry, side, arguments, source, line, sha, status))))

    for path in sorted(source_files()):
        if ".Tests" in str(path) or "Tests/" in relative(path) or "TestMod/" in relative(path):
            continue
        text = path.read_text(encoding="utf-8-sig")
        # Remove comments without removing strings. Inventory remains a lexical index,
        # not proof that DI/Harmony registers a declaration at runtime.
        token = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/'
        code = re.sub(token, lambda m: re.sub(r"[^\n]", " ", m[0]) if m[0].startswith(("//", "/*")) else m[0], text)
        sha = digest(path)
        src = relative(path)
        area = domain(Path(src))
        commands = list(re.finditer(r"\bclass\s+(\w+)\s*:\s*ICoopCommand\b", code))
        for index, match in enumerate(commands):
            block = code[match.end():commands[index + 1].start() if index + 1 < len(commands) else len(code)]
            prefix = re.search(r'\bPrefix\s*=>\s*"([^"]+)"', block)
            name = re.search(r'\bName\s*=>\s*"([^"]+)"', block)
            side = re.search(r"\bSide\s*=>\s*CoopCommandSide\.(\w+)", block)
            header = block.split("ProcessCommand", 1)[0]
            args = re.findall(r'new\s+(?:\w*ExpectedArgs)\s*\(\s*"([^"]+)"', header)
            key = prefix[1] + "." + name[1] if prefix and name else match[1]
            add("command", area, key, match[1], side[1] if side else "unresolved", "; ".join(args),
                src, code.count("\n", 0, match.start()) + 1, sha, "declaration-only")
        if not src.startswith(("source/Coop/", "source/GameInterface/", "source/Missions/", "source/Coop.Core/")):
            continue
        for line_number, line in enumerate(code.splitlines(), 1):
            legacy = re.search(r'CommandLineArgumentFunction\("([^"]+)",\s*"([^"]+)"\)', line)
            if legacy:
                add("legacy-command", area, legacy[2] + "." + legacy[1], "console registry", "inspect-source", "inspect-source",
                    src, line_number, sha, "declaration-only")
            sync = re.search(r"\b\w*[Ss]ync\w*\.(Add\w+)\((.*)", line)
            if sync:
                expression = sync[2].rstrip().rstrip(";")
                add("sync-registration", area, src + ":" + str(line_number), sync[1], "inspect-producer", expression,
                    src, line_number, sha, "declaration-only")
            patch = re.search(r"\[HarmonyPatch(?:\((.*)\))?\]", line)
            if patch:
                add("patch-target", area, src + ":" + str(line_number), patch[1] or "dynamic TargetMethods", "inspect-prefix",
                    "", src, line_number, sha, "declaration-only")

    snapshot = json.loads((FEATURES / "baseline/installed.json").read_text(encoding="utf-8"))
    sources = {item["key"]: item for item in snapshot["sources"]}
    allowlist_path = ROOT / "source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs"
    gate = allowlist_path.read_text(encoding="utf-8-sig").split("Allowlist =", 1)[1].split("};", 1)[0]
    allowlist = set(re.findall(r"typeof\((\w+)\)", gate))
    for behavior in snapshot["behaviors"]:
        source = sources[behavior["registrar"]]
        issue = bool(re.search(r"Issue(?:Quest)?Behavior$", behavior["type"])) and behavior["type"] != "IssuesCampaignBehavior"
        status = ("allowlisted-source-only" if behavior["type"] in allowlist else "disabled-by-issue-gate") if issue else "installed-registration"
        add("vanilla-issue" if issue else "vanilla-behavior", "Issues" if issue else "Campaign",
            behavior["type"], source["type"], "vanilla", "", source["assembly"], behavior["line"], source["sha256"], status)
    for perk in snapshot["perks"]:
        source = sources["perks"]
        args = f"tier={perk['tier']}; alternative={perk['alternative']}; roles={','.join(perk['roles'])}; bonuses={','.join(perk['bonuses'])}; increments={','.join(perk['increments'])}"
        add("vanilla-perk", perk["skill"], perk["id"], "Character / perk selection", "role-dependent", args,
            source["assembly"], perk["line"], source["sha256"], "installed-definition")
    for menu in snapshot["menus"]:
        source = sources[menu["registrar"]]
        add("vanilla-menu-option", menu["domain"], menu["menu"] + "." + menu["option"], menu["method"], "client-input",
            menu["callbacks"], source["assembly"], menu["line"], source["sha256"], "installed-definition")
    return sorted(rows, key=lambda row: (row["kind"], row["domain"], row["id"], row["source"], int(row["line"])))


def serialize(rows):
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=COLUMNS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return buffer.getvalue()


def catalog_rows():
    with (FEATURES / "catalog.csv").open(encoding="utf-8", newline="") as stream:
        return list(csv.DictReader(stream))


def behavior_rows():
    with (FEATURES / "behavior-leaves.csv").open(encoding="utf-8", newline="") as stream:
        return list(csv.DictReader(stream))


def source_path_rows():
    with (FEATURES / "source-paths.csv").open(encoding="utf-8", newline="") as stream:
        return list(csv.DictReader(stream))


def render_source_paths():
    rows = source_path_rows()
    index = ["# Installed source paths", "",
             "Generated structural navigation from [source-paths.csv](source-paths.csv).",
             "These paths are not interpreted behavior leaves or gameplay passes. Guards and local returns",
             "are recorded with their complete containing member; aliases, enclosing predicates, switch",
             "selectors, indirect calls, RNG and caller context must be interpreted before deriving an oracle.",
             "Assignments and calls inside nested branches are located, not asserted unconditional.",
             "DialogFlow delegates, expression-bodied properties, ternaries and native callbacks are not",
             "exhaustively expanded by this lexical source index. See [granularity](granularity.md).", "",
             "| Installed type | Paths | Normal support |", "| --- | ---: | --- |"]
    pages = {}
    for typ in sorted({r["evidence"].split("@", 1)[0] for r in rows}):
        selected = [r for r in rows if r["evidence"].split("@", 1)[0] == typ]
        short = typ.split(".")[-1]
        support = ", ".join(sorted({r["support"] for r in selected}))
        index.append(f"| [{typ}](source-paths/{short}.md) | {len(selected)} | {support} |")
        lines = ["# " + short, "", "Installed type: `" + typ + "`.", "",
                 "Normal support: " + support + ".",
                 "[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)", "",
                 "This is structural navigation. Local returns, assignments and calls can belong to nested",
                 "or exclusive branches; resolve the complete caller/guard context before using any as an outcome.", "",
                 "| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |",
                 "| --- | --- | --- | --- | --- | --- | --- |"]
        for row in selected:
            lines.append("| " + " | ".join(cell(row[k]) for k in ("id", "parent", "preconditions", "expected", "state_changes", "side_effects", "evidence")) + " |")
        pages[FEATURES / "source-paths" / (short + ".md")] = "\n".join(lines) + "\n"
    index.extend(["", f"{len(rows)} structural paths; {len(pages)} containing types. Neither count is a gameplay pass.", ""])
    pages[FEATURES / "source-paths.md"] = "\n".join(index)
    return pages


def render_behaviors():
    rows = behavior_rows()
    groups = {row["id"]: row for row in catalog_rows()}
    counts = Counter(row["domain"] for row in rows)
    index = ["# Individual behavior leaves", "",
             "The [action catalog](../catalog.csv) supplies navigation groups. Each leaf below has its own",
             "precondition and outcome in [behavior-leaves.csv](../behavior-leaves.csv).",
             "[Source records](../baseline/behavior-sources.json) bind installed type, DLL hash and source span.",
             "Read [the mapping contract](../granularity.md) before using a leaf as an acceptance oracle.", "",
             "[Structural source paths](../source-paths.md) are a separate navigation inventory and are excluded",
             "from behavior-leaf counts. Only interpreted cases have established source outcomes; definition",
             "and candidate leaves retain their own unresolved work. No aggregate inherits a leaf's pass.", "",
             "| Area | Leaves | Interpreted | Definitions | Candidate |", "| --- | ---: | ---: | ---: | ---: |"]
    pages = {}
    for area in sorted(counts):
        selected = [row for row in rows if row["domain"] == area]
        statuses = Counter(row["knowledge"] for row in selected)
        index.append(f"| [{area}]({area}.md) | {len(selected)} | {statuses['source-interpreted']} | {statuses['definition-inspected']} | {statuses['candidate']} |")
        lines = ["# " + area + " behavior leaves", "", "Generated from [behavior-leaves.csv](../behavior-leaves.csv).",
                 "[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)", ""]
        for parent in dict.fromkeys(row["parent"] for row in selected):
            leaves = [row for row in selected if row["parent"] == parent]
            lines.extend(["## " + parent + " " + groups.get(parent, {}).get("feature", "unknown parent"), "",
                          "Each entry retains authority and separate owner/observer observations in the CSV.", ""])
            for row in leaves:
                lines.extend(["### " + row["id"], "", row["behavior"].strip(), "",
                              "- Entry/action: " + cell(row["entry"] + "; " + row["action"]),
                              "- Preconditions: " + cell(row["preconditions"]),
                              "- Expected: " + cell(row["expected"]),
                              "- State: " + cell(row["state_changes"]),
                              "- Side effects: " + cell(row["side_effects"]),
                              "- Authority: " + cell(row["authority"]),
                              "- Owner observation: " + cell(row["owner_observation"]),
                              "- Other client: " + cell(row["observer_observation"]),
                              "- Boundary/negative: " + cell(row["negative_case"]),
                              "- Persistence: " + cell(row["persistence"]),
                              "- Evidence: " + cell(row["knowledge"] + "; runtime " + row["runtime"] + "; support " + row["support"]),
                              "- Source span: " + cell(row["evidence"] or "exact installed member unresolved"),
                              "- Remaining: " + cell(row["gap"]), ""])
                pointers = row["source"].split(";")
                lines.extend(["Sources: " + ", ".join(f"[{p}](../../{p})" for p in pointers), ""])
                if row["recipe"]:
                    lines.extend([f"Recipe: [{row['recipe']}](../{row['recipe']})", ""])
        pages[FEATURES / "behaviors" / (area + ".md")] = "\n".join(lines).rstrip() + "\n"
    index.extend(["", "Totals by knowledge: " + ", ".join(f"{k}={v}" for k, v in sorted(Counter(r['knowledge'] for r in rows).items())),
                  "Runtime states: " + ", ".join(f"{k}={v}" for k, v in sorted(Counter(r['runtime'] for r in rows).items())), ""])
    pages[FEATURES / "behaviors/README.md"] = "\n".join(index)
    return pages


def cell(value):
    return html.escape(str(value).strip(), quote=False).replace("|", "&#124;").replace("\n", " ")


def render_perks():
    effects = json.loads((FEATURES / "baseline/perk-effects.json").read_text(encoding="utf-8"))["effects"]
    index = ["# Perk choices and individual effects", "",
             "Each effect retains its declared role, raw bonus, increment and troop mask independently.",
             "The skill threshold comes from the installed definition, not a measured UI eligibility result.",
             "`{VALUE}` is a game's localization variable; raw bonuses are not automatically formatted percentages.",
             "Consumer references locate mentions of the perk. A mention is not an established primary/secondary",
             "apply path or evidence that co-op uses that model. Missing references do not establish an unused perk.",
             "[Definition data](baseline/perk-effects.json) · [Mapping contract](granularity.md) · [Behavior leaves](behaviors/progression.md)", "",
             "| Skill | Choices | Effects |", "| --- | ---: | ---: |"]
    pages = {}
    for skill in sorted({row["skill"] for row in effects}):
        selected = [row for row in effects if row["skill"] == skill]
        choices = list(dict.fromkeys(row["id"] for row in selected))
        index.append(f"| [{skill}](perks/{skill}.md) | {len(choices)} | {len(selected)} |")
        lines = ["# " + skill + " perks", "", "[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)", "",
                 "All entries are definition-inspected; runtime is unrun. The definitions, selection and effects",
                 "are separate leaves. Located consumer mentions still need full caller/role/context interpretation.", ""]
        for key in choices:
            own = [row for row in selected if row["id"] == key]
            first = own[0]
            lines.extend(["## " + key, "",
                          f"Choice leaf: `perk.{key}.choice`. Required skill: {first['required_skill']}; alternative: `{first['alternative']}`.",
                          f"Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:{first['definition_line']}`.", "",
                          "| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |",
                          "| --- | --- | --- | --- | --- | --- |"])
            for row in own:
                lines.append("| " + " | ".join(cell(value) for value in
                    ("perk." + key + "." + row["effect"], row["description"], row["role"], row["bonus"], row["increment"], row["troop_mask"])) + " |")
            uses = first["uses"]
            if uses:
                lines.extend(["", "Located consumer mentions, shared as unresolved candidates for both effects:", "",
                              "| Containing member | Mention line | Complete member ends |", "| --- | ---: | ---: |"])
                for use in uses:
                    short = use["type"].split(".")[-1]
                    lines.append(f"| [{use['type']}.{use['member']}](../source-paths/{short}.md) | {use['line']} | {use['end_line']} |")
            else:
                lines.extend(["", "No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved."])
            lines.extend(["", "Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.",
                          "Observe the exact affected state on the owner and permitted shared consequence on the other real client.",
                          "Persistence of the selection does not establish persistence or application of this effect.", ""])
        pages[FEATURES / "perks" / (skill + ".md")] = "\n".join(lines)
    pages[FEATURES / "perks.md"] = "\n".join(index) + "\n"
    return pages


def render_actions():
    rows = catalog_rows()
    lines = ["# Player action groups", "", "Generated from [catalog.csv](catalog.csv) by `python tools/feature_map.py refresh`.",
             "Use [individual behavior leaves](behaviors/README.md) for separately identified cases and effects.",
             "Oracles below are acceptance requirements, not recorded passing results. A candidate is a coverage question;",
             "neither its presence nor a nearby declaration establishes the feature's exact installed behavior or co-op support.",
             "Source-inspected rows identify repository surfaces only. See [evidence](evidence/authoring.md) for actual exercise.", ""]
    for area in dict.fromkeys(row["domain"] for row in rows):
        selected = [row for row in rows if row["domain"] == area]
        lines.extend(["## " + area, "", "Entry: " + selected[0]["entry"], "",
                      "| ID | Action | Independent slices | Required observation | Support / evidence |", "| --- | --- | --- | --- | --- |"])
        for row in selected:
            lines.append("| " + " | ".join(cell(row[key]) for key in ("id", "feature", "variants", "oracle")) + " | " + cell(row["support"] + " / " + row["status"]) + " |")
        pointers = sorted({item.strip() for row in selected for item in row["source"].split(";")})
        lines.extend(["", "Sources: " + ", ".join(f"[{path}](../{path})" for path in pointers), ""])
    return "\n".join(lines).rstrip() + "\n"


def render_installed(rows):
    lines = ["# Installed Bannerlord definitions", "", "Generated from [baseline/installed.json](baseline/installed.json).",
             "This is an independently inspected Native v1.4.8 snapshot. Definitions and registration do not prove gameplay support.",
             "Line numbers refer to the named decompiled type; owning assembly hashes are in [provenance](provenance.md).", "",
             "## Normal issue availability", "", "The current source allowlist is applied separately from the installed definitions.",
             "Debug grant catalog entries are not an exception to normal gameplay gating. The quest journal is separately blocked",
             "by `GameUIDisable.PushStatePatch` for `QuestsState`.", "",
             "| Behavior | Normal co-op gate | Registrar / line |", "| --- | --- | --- |"]
    for row in rows:
        if row["kind"] == "vanilla-issue":
            lines.append(f"| {row['id']} | {row['status']} | {row['entry']}:{row['line']} |")
    lines.extend(["", "## Menu choices", "", "One row per installed `AddGameMenuOption` call from three inspected menu registrars.",
                  "Conditions and consequences are named, not assumed successful. Conditional naval/cheat/access paths are included",
                  "as declarations; enabled module/license/access state still needs observation.", "",
                  "| Domain | Menu / option | Condition / consequence | Source line |", "| --- | --- | --- | --- |"])
    for row in rows:
        if row["kind"] == "vanilla-menu-option":
            lines.append("| " + " | ".join(cell(row[key]) for key in ("domain", "id", "arguments", "line")) + " |")
    lines.extend(["", "## Individual perks", "", "Every row is a separately initialized perk. Retain both role-dependent effects and alternative choice.",
                  "[Perk effect sheets](perks.md) expand primary/secondary effects, troop masks and located consumer references.",
                  "`GetTierCost(n)` is an installed expression, not a copied assertion about the player's current level.",
                  "Bonus numbers below are raw declarations: `AddFactor` is not an already measured percentage result.",
                  "Zero bonuses can enable a capability and must not be treated as absent effects. Applicable models, troop flags,",
                  "hero role, ownership, and actual effect still need inspection and a real action before claiming perk support."])
    perks = [row for row in rows if row["kind"] == "vanilla-perk"]
    for skill in sorted({row["domain"] for row in perks}):
        lines.extend(["", "### " + skill, "", "| Perk ID | Choice / role / raw bonus / increment | Definition line |", "| --- | --- | --- |"])
        for row in perks:
            if row["domain"] == skill:
                lines.append(f"| {row['id']} | {cell(row['arguments'])} | {row['line']} |")
    lines.extend(["", "## Other campaign registrations", "", "These are registrations from `SandBoxManager` and `SandBoxSubModule`, not an exhaustive game-wide list.",
                  "StoryMode, custom battle, native multiplayer, native engine behavior, and optional modules need separate expansion.",
                  "", "| Registered behavior | Registrar / line |", "| --- | --- |"])
    for row in rows:
        if row["kind"] == "vanilla-behavior":
            lines.append(f"| {row['id']} | {row['entry']}:{row['line']} |")
    return "\n".join(lines) + "\n"


def generated_files():
    rows = inventory_rows()
    return {INVENTORY: serialize(rows), FEATURES / "player-actions.md": render_actions(),
            FEATURES / "installed.md": render_installed(rows), **render_source_paths(),
            **render_behaviors(), **render_perks()}


def validate():
    errors = []
    for path, expected in generated_files().items():
        if not path.exists() or path.read_text(encoding="utf-8") != expected:
            errors.append(relative(path) + " differs from current input; review changes then run refresh")
    catalog = catalog_rows()
    groups = {row["id"]: row for row in catalog}
    ids = [row["id"] for row in catalog]
    if len(ids) != len(set(ids)):
        errors.append("duplicate feature IDs")
    leaves = behavior_rows()
    paths = source_path_rows()
    snapshot = json.loads((FEATURES / "baseline/installed.json").read_text(encoding="utf-8"))
    source_records = json.loads((FEATURES / "baseline/behavior-sources.json").read_text(encoding="utf-8"))["sources"]
    leaf_ids = [row["id"] for row in leaves]
    if len(leaf_ids) != len(set(leaf_ids)):
        errors.append("duplicate behavior leaf IDs")
    path_ids = [row["id"] for row in paths]
    if len(path_ids) != len(set(path_ids)) or set(leaf_ids) & set(path_ids):
        errors.append("duplicate/colliding structural path IDs")
    structural = [dict(row, knowledge="source-structural", runtime="unrun", source="features/baseline/behavior-sources.json",
                       entry="Complete containing member", action="Interpret source context", authority="Vanilla source only",
                       owner_observation="unresolved", observer_observation="unresolved", negative_case="unresolved",
                       persistence="unresolved", gap="structural navigation, not behavioral proof", recipe="") for row in paths]
    for row in [*leaves, *structural]:
        for field in ("id", "kind", "parent", "domain", "behavior", "entry", "preconditions", "action", "expected",
                      "state_changes", "side_effects", "authority", "owner_observation", "observer_observation",
                      "negative_case", "persistence", "knowledge", "runtime", "support", "source", "gap"):
            if not row.get(field):
                errors.append(row.get("id", "unknown") + ": missing leaf " + field)
        if row["parent"] not in groups:
            errors.append(row["id"] + ": unknown parent")
        elif groups[row["parent"]]["domain"] != row["domain"]:
            errors.append(row["id"] + ": parent domain differs")
        if row["knowledge"] not in ("candidate", "source-structural", "source-interpreted", "definition-inspected"):
            errors.append(row["id"] + ": invalid leaf knowledge")
        if row["runtime"] not in ("unrun", "blocked", "exercised"):
            errors.append(row["id"] + ": invalid leaf runtime")
        if row["knowledge"] != "candidate" and not row["evidence"]:
            errors.append(row["id"] + ": missing source span")
        for reference in row["evidence"].split(";"):
            if not reference:
                continue
            match = re.fullmatch(r"(.+)@(\d+)-(\d+)", reference)
            if not match or match[1] not in source_records:
                errors.append(row["id"] + ": unknown evidence")
            elif not 1 <= int(match[2]) <= int(match[3]) <= source_records[match[1]]["line_count"]:
                errors.append(row["id"] + ": invalid evidence range")
        for path in row["source"].split(";"):
            resolved = (ROOT / path).resolve()
            if not resolved.is_relative_to(ROOT) or not resolved.exists():
                errors.append(row["id"] + ": missing/outside leaf source " + path)
        if row["recipe"] and not (FEATURES / row["recipe"]).is_file():
            errors.append(row["id"] + ": missing leaf recipe")
    if any(row["knowledge"] == "source-structural" for row in leaves):
        errors.append("structural paths must be separate from behavior leaves")
    missing_parents = set(ids) - {row["parent"] for row in leaves}
    if missing_parents:
        errors.append("groups without behavior leaves: " + ", ".join(sorted(missing_parents)))
    installed_hashes = {row["assembly"]: row["sha256"] for row in snapshot["sources"]}
    for key, source in source_records.items():
        if source["type"] != key:
            errors.append(key + ": behavior source identity differs")
        if not re.fullmatch(r"[a-f0-9]{64}", source["sha256"]):
            errors.append(key + ": invalid behavior source digest")
        if not re.fullmatch(r"[a-f0-9]{64}", source["decompiled_sha256"]):
            errors.append(key + ": invalid complete-source digest")
        if source["line_count"] < 1:
            errors.append(key + ": empty behavior source")
        if source["provider"] == "repository":
            path = (ROOT / source["assembly"]).resolve()
            if not path.is_relative_to(ROOT) or not path.is_file() or digest(path) != source["sha256"]:
                errors.append(key + ": repository behavior source changed")
        elif installed_hashes.get(source["assembly"]) != source["sha256"]:
            errors.append(key + ": behavior source differs from installed baseline")
    effects = json.loads((FEATURES / "baseline/perk-effects.json").read_text(encoding="utf-8"))["effects"]
    effect_ids = ["perk." + row["id"] + "." + row["effect"] for row in effects]
    if len(effect_ids) != len(set(effect_ids)):
        errors.append("duplicate perk effects")
    if {row["id"] for row in effects} != {row["id"] for row in snapshot["perks"]}:
        errors.append("perk effect catalog differs from installed choices")
    if set(effect_ids) != {row["id"] for row in leaves if row["kind"] == "perk-effect"}:
        errors.append("perk effect leaves differ from definition records")
    for row in effects:
        if row["effect"] not in ("primary", "secondary") or not row["description"]:
            errors.append(row["id"] + ": invalid perk effect definition")
        for use in row["uses"]:
            source = source_records.get(use["type"])
            if not source or not 1 <= use["line"] <= use["end_line"] <= source["line_count"]:
                errors.append(row["id"] + ": invalid perk consumer range")
    curated = json.loads((FEATURES / "interpreted-cases.json").read_text(encoding="utf-8"))
    cases = {row["id"]: row for row in leaves if row["kind"] == "interpreted-case"}
    if len({row["key"] for row in curated}) != len(curated) or set(cases) != {row["key"] for row in curated}:
        errors.append("interpreted case leaves differ from curated records")
    for row in curated:
        leaf = cases.get(row["key"], {})
        if any(str(value) != leaf.get(field) for field, value in row.items() if field != "key"):
            errors.append(row["key"] + ": interpreted case content differs")
    for row in catalog:
        for field in ("id", "domain", "feature", "entry", "variants", "oracle", "support", "status", "source"):
            if not row.get(field):
                errors.append(row.get("id", "unknown") + ": missing " + field)
        if row.get("status") not in ("source-inspected", "installed-definition", "candidate", "exercised", "blocked"):
            errors.append(row["id"] + ": invalid status")
        for path in row["source"].split(";"):
            resolved = (ROOT / path.strip()).resolve()
            if not resolved.is_relative_to(ROOT) or not resolved.exists():
                errors.append(row["id"] + ": missing/outside source " + path)
        if row.get("recipe") and not (FEATURES / row["recipe"]).is_file():
            errors.append(row["id"] + ": missing recipe")
    for path in [ROOT / ".agents/skills/verify-bannerlord/SKILL.md", *FEATURES.rglob("*.md")]:
        text = path.read_text(encoding="utf-8")
        for target in re.findall(r"\]\(([^)]+)\)", text):
            if "://" in target or target.startswith("#"):
                continue
            target = target.split("#", 1)[0]
            resolved = (path.parent / target).resolve()
            if not resolved.is_relative_to(ROOT) or not resolved.exists():
                errors.append(relative(path) + ": broken/outside link " + target)
        if re.search(r"(?:[A-Za-z]:[\\/]|/home/|/mnt/|\[TODO|<placeholder)", text):
            errors.append(relative(path) + ": local path or unfinished scaffold")
    for source in snapshot["sources"]:
        if not re.fullmatch(r"[a-f0-9]{64}", source["sha256"]):
            errors.append("invalid installed source digest")
        if not re.match(r"(?:bin/Win64_Shipping_Client/TaleWorlds\.|Modules/(?:SandBox|StoryMode|Native|SandBoxCore|CustomBattle|Multiplayer)/)", source["assembly"]):
            errors.append("installed source outside baseline allowlist")
    if errors:
        raise SystemExit("\n".join(errors))
    counts = Counter(row["kind"] for row in inventory_rows())
    print(f"validated {len(catalog)} action groups, {len(leaves)} behavior leaves, {len(set(row['domain'] for row in catalog))} domains")
    print(f"{len(paths)} structural paths are indexed separately; " + json.dumps(dict(Counter(row['knowledge'] for row in leaves)), sort_keys=True))
    print(json.dumps(dict(sorted(counts.items())), sort_keys=True))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("refresh", "check", "find"))
    parser.add_argument("query", nargs="?", default="")
    parser.add_argument("--kind", default="")
    args = parser.parse_args()
    if args.action == "refresh":
        generated = generated_files()
        for path, content in generated.items():
            if not path.resolve().is_relative_to(FEATURES):
                raise SystemExit("generated page outside features")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8", newline="")
        print(f"refreshed {len(generated)} inventory/readable files")
    elif args.action == "check":
        validate()
    else:
        rows = [*csv.DictReader((FEATURES / "catalog.csv").open(encoding="utf-8", newline="")),
                *csv.DictReader(INVENTORY.open(encoding="utf-8", newline="")), *behavior_rows(), *source_path_rows()]
        for row in rows:
            if args.kind and row.get("kind") != args.kind:
                continue
            if args.query.casefold() in " ".join(str(value) for value in row.values()).casefold():
                print(json.dumps(row, ensure_ascii=False))


if __name__ == "__main__":
    main()
