"""Read, refresh and validate the Bannerlord feature map. Never builds or launches a game."""

import argparse
import csv
import hashlib
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


def cell(value):
    return str(value).replace("|", "&#124;").replace("\n", " ")


def render_actions():
    rows = catalog_rows()
    lines = ["# Player action coverage", "", "Generated from [catalog.csv](catalog.csv) by `python tools/feature_map.py refresh`.",
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
            FEATURES / "installed.md": render_installed(rows)}


def validate():
    errors = []
    for path, expected in generated_files().items():
        if not path.exists() or path.read_text(encoding="utf-8") != expected:
            errors.append(relative(path) + " differs from current input; review changes then run refresh")
    catalog = catalog_rows()
    ids = [row["id"] for row in catalog]
    if len(ids) != len(set(ids)):
        errors.append("duplicate feature IDs")
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
    snapshot = json.loads((FEATURES / "baseline/installed.json").read_text(encoding="utf-8"))
    for source in snapshot["sources"]:
        if not re.fullmatch(r"[a-f0-9]{64}", source["sha256"]):
            errors.append("invalid installed source digest")
        if not re.match(r"(?:bin/Win64_Shipping_Client/TaleWorlds\.|Modules/(?:SandBox|StoryMode|Native|SandBoxCore|CustomBattle|Multiplayer)/)", source["assembly"]):
            errors.append("installed source outside baseline allowlist")
    if errors:
        raise SystemExit("\n".join(errors))
    counts = Counter(row["kind"] for row in inventory_rows())
    print(f"validated {len(catalog)} behavioral rows, {len(set(row['domain'] for row in catalog))} domains")
    print(json.dumps(dict(sorted(counts.items())), sort_keys=True))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("refresh", "check", "find"))
    parser.add_argument("query", nargs="?", default="")
    parser.add_argument("--kind", default="")
    args = parser.parse_args()
    if args.action == "refresh":
        for path, content in generated_files().items():
            path.write_text(content, encoding="utf-8", newline="")
            print("refreshed " + relative(path))
    elif args.action == "check":
        validate()
    else:
        rows = [*csv.DictReader((FEATURES / "catalog.csv").open(encoding="utf-8", newline="")),
                *csv.DictReader(INVENTORY.open(encoding="utf-8", newline=""))]
        for row in rows:
            if args.kind and row.get("kind") != args.kind:
                continue
            if args.query.casefold() in " ".join(str(value) for value in row.values()).casefold():
                print(json.dumps(row, ensure_ascii=False))


if __name__ == "__main__":
    main()
