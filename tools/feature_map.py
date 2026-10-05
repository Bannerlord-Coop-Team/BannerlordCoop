"""Query and validate the Bannerlord feature map. Never builds or launches a game."""

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
    return html.escape(str(value).strip(), quote=False).replace("|", "&#124;").replace("\n", " ")


def render_actions():
    rows = catalog_rows()
    lines = ["# Player-visible behaviors", "", "Generated from [catalog.csv](catalog.csv) by `python tools/feature_map.py refresh`.",
             "Material outcome branches are retained in [source-cases.csv](source-cases.csv).",
             "Oracles below are acceptance requirements, not recorded passing results. A candidate is a coverage question;",
             "neither its presence nor a nearby declaration establishes the feature's exact installed behavior or co-op support.",
             "Source-inspected rows identify repository surfaces only. See [evidence](evidence/authoring.md) for actual exercise.", ""]
    for area in dict.fromkeys(row["domain"] for row in rows):
        selected = [row for row in rows if row["domain"] == area]
        lines.extend(["## " + area, "", "Entry: " + selected[0]["entry"], "",
                      "| ID | Action | Outcome variants | Required observation | Support / evidence |", "| --- | --- | --- | --- | --- |"])
        for row in selected:
            lines.append("| " + " | ".join(cell(row[key]) for key in ("id", "feature", "variants", "oracle")) + " | " + cell(row["support"] + " / " + row["status"]) + " |")
        pointers = sorted({item.strip() for row in selected for item in row["source"].split(";")})
        lines.extend(["", "Sources: " + ", ".join(f"[{path}](../{path})" for path in pointers), ""])
    return "\n".join(lines).rstrip() + "\n"


def case_rows():
    with (FEATURES / "source-cases.csv").open(encoding="utf-8", newline="") as stream:
        return list(csv.DictReader(stream))


def validate():
    errors = []
    readable = FEATURES / "player-actions.md"
    if not readable.exists() or readable.read_text(encoding="utf-8") != render_actions():
        errors.append("features/player-actions.md differs from current catalog; review changes then run refresh")
    catalog = catalog_rows()
    groups = {row["id"]: row for row in catalog}
    cases = case_rows()
    if len(groups) != len(catalog):
        errors.append("duplicate feature IDs")
    if len({row["id"] for row in cases}) != len(cases):
        errors.append("duplicate source case IDs")
    snapshot = json.loads((FEATURES / "baseline/installed.json").read_text(encoding="utf-8"))
    sources = json.loads((FEATURES / "baseline/behavior-sources.json").read_text(encoding="utf-8"))["sources"]
    for row in catalog:
        for field in ("id", "domain", "feature", "entry", "variants", "oracle", "support", "status", "source"):
            if not row.get(field):
                errors.append(row.get("id", "unknown") + ": missing " + field)
        if row.get("status") not in ("source-inspected", "installed-definition", "candidate", "exercised", "blocked"):
            errors.append(row["id"] + ": invalid status")
        for name in row["source"].split(";"):
            resolved = (ROOT / name.strip()).resolve()
            if not resolved.is_relative_to(ROOT) or not resolved.exists():
                errors.append(row["id"] + ": missing/outside source " + name)
        if row.get("recipe") and not (FEATURES / row["recipe"]).is_file():
            errors.append(row["id"] + ": missing recipe")
    for row in cases:
        for field in ("id", "parent", "behavior", "preconditions", "expected", "state_changes", "side_effects",
                      "negative_case", "evidence", "authority", "support", "runtime", "gap"):
            if not row.get(field):
                errors.append(row.get("id", "unknown") + ": missing source case " + field)
        if row["parent"] not in groups:
            errors.append(row["id"] + ": unknown parent")
        if row["runtime"] not in ("unrun", "blocked", "exercised"):
            errors.append(row["id"] + ": invalid runtime")
        for reference in row["evidence"].split(";"):
            match = re.fullmatch(r"(.+)@(\d+)-(\d+)", reference)
            if not match or match[1] not in sources:
                errors.append(row["id"] + ": unknown evidence")
            elif not 1 <= int(match[2]) <= int(match[3]) <= sources[match[1]]["line_count"]:
                errors.append(row["id"] + ": invalid evidence range")
    installed_hashes = {row["assembly"]: row["sha256"] for row in snapshot["sources"]}
    for key, source in sources.items():
        if source["type"] != key:
            errors.append(key + ": source identity differs")
        for field in ("sha256", "decompiled_sha256"):
            if not re.fullmatch(r"[a-f0-9]{64}", source[field]):
                errors.append(key + ": invalid source digest")
        if source["line_count"] < 1:
            errors.append(key + ": empty source")
        if source["provider"] == "repository":
            resolved = (ROOT / source["assembly"]).resolve()
            if not resolved.is_relative_to(ROOT) or not resolved.is_file() or digest(resolved) != source["sha256"]:
                errors.append(key + ": repository behavior source changed")
        elif installed_hashes.get(source["assembly"]) != source["sha256"]:
            errors.append(key + ": source differs from installed baseline")
    for source in snapshot["sources"]:
        if not re.fullmatch(r"[a-f0-9]{64}", source["sha256"]):
            errors.append("invalid installed source digest")
        if not re.match(r"(?:bin/Win64_Shipping_Client/TaleWorlds\.|Modules/(?:SandBox|StoryMode|Native|SandBoxCore|CustomBattle|Multiplayer)/)", source["assembly"]):
            errors.append("installed source outside baseline allowlist")
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
    if errors:
        raise SystemExit("\n".join(errors))
    print(f"validated {len(catalog)} behaviors in {len({row['domain'] for row in catalog})} areas and {len(cases)} source cases")
    print("runtime: " + json.dumps(dict(Counter(row["runtime"] for row in cases)), sort_keys=True))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("refresh", "check", "find", "inventory"))
    parser.add_argument("query", nargs="?", default="")
    parser.add_argument("--kind", default="")
    args = parser.parse_args()
    if args.action == "refresh":
        (FEATURES / "player-actions.md").write_text(render_actions(), encoding="utf-8", newline="")
        print("refreshed player-actions.md; source inventories remain on demand")
    elif args.action == "check":
        validate()
    elif args.action == "inventory":
        rows = [row for row in inventory_rows() if not args.kind or row["kind"] == args.kind]
        print(serialize(rows), end="")
    else:
        rows = [dict(row, kind="feature") for row in catalog_rows()]
        rows.extend(dict(row, kind="interpreted-case", knowledge="source-interpreted") for row in case_rows())
        if args.kind not in ("feature", "interpreted-case"):
            rows.extend(inventory_rows())
        for row in rows:
            if args.kind and row["kind"] != args.kind:
                continue
            if args.query.casefold() in " ".join(str(value) for value in row.values()).casefold():
                print(json.dumps(row, ensure_ascii=False))


if __name__ == "__main__":
    main()
