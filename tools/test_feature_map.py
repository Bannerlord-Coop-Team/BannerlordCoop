"""Focused checks for feature discovery and stale/broken evidence rejection. No game processes."""

import contextlib
import csv
import io
import json
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import feature_map


class FeatureMapTests(unittest.TestCase):
    def test_perk_effect_roles_masks_and_single_effect_are_distinct(self):
        definitions = json.loads((feature_map.FEATURES / "baseline/perk-effects.json").read_text(encoding="utf-8"))["effects"]
        effects = {(row["id"], row["effect"]): row for row in definitions}
        primary = effects["OneHandedWrappedHandles", "primary"]
        secondary = effects["OneHandedWrappedHandles", "secondary"]
        self.assertEqual((primary["role"], primary["bonus"], primary["increment"], primary["troop_mask"]),
                         ("Personal", "0.2f", "AddFactor", "TroopUsageFlags.Undefined"))
        self.assertEqual((secondary["role"], secondary["bonus"], secondary["increment"], secondary["troop_mask"]),
                         ("Captain", "30f", "Add", "TroopUsageFlags.OneHandedUser"))
        self.assertEqual(effects["OneHandedDuelist", "primary"]["role"], "Personal")
        self.assertEqual(effects["OneHandedDuelist", "secondary"]["role"], "Personal")
        self.assertIn(("EngineeringMasterwork", "primary"), effects)
        self.assertNotIn(("EngineeringMasterwork", "secondary"), effects)
        self.assertTrue(any(use["type"].endswith("SandboxAgentStatCalculateModel") for use in secondary["uses"]))
        roles = {"Scout": "progression.014", "Engineer": "progression.015", "Quartermaster": "progression.016",
                 "Surgeon": "progression.017", "ArmyCommander": "progression.018", "PartyMember": "progression.019"}
        leaves = {row["id"]: row for row in feature_map.behavior_rows()}
        for definition in definitions:
            if definition["role"] in roles:
                leaf = leaves["perk." + definition["id"] + "." + definition["effect"]]
                self.assertEqual(leaf["parent"], roles[definition["role"]])

    def test_real_command_contract_and_commented_sync(self):
        rows = feature_map.inventory_rows()
        commands = {row["id"]: row for row in rows if row["kind"] == "command"}
        join = commands["coop.debug.tournaments.danustica_request_join"]
        self.assertEqual((join["side"], join["arguments"]), ("Client", ""))
        lifecycle = commands["coop.debug.town.apply_garrison_lifecycle"]
        self.assertEqual((lifecycle["side"], lifecycle["arguments"]), ("Server", "townId; operation"))
        town = [row["arguments"] for row in rows if row["kind"] == "sync-registration"
                and row["source"] == "source/GameInterface/Services/Towns/TownSync.cs"]
        self.assertTrue(any("Town.Governor" in value for value in town))
        self.assertFalse(any("Town._tradeTax" in value for value in town))
        self.assertFalse(any("Town.TradeTaxAccumulated" in value for value in town))

    def test_source_and_link_drift_are_rejected_in_isolated_copy(self):
        original = feature_map.ROOT
        sources = feature_map.source_files()
        with tempfile.TemporaryDirectory(prefix="bannerlord-map-check-") as temporary:
            root = Path(temporary)
            shutil.copytree(original / "features", root / "features")
            shutil.copytree(original / ".agents", root / ".agents")
            for path in sources:
                target = root / path.relative_to(original)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
            # Preserve all real linked instructions/paths without copying game/package outputs.
            for path in (original / "features").rglob("*.md"):
                for target in feature_map.re.findall(r"\]\(([^)]+)\)", path.read_text(encoding="utf-8")):
                    if "://" in target or target.startswith("#"):
                        continue
                    source = (path.parent / target.split("#", 1)[0]).resolve()
                    if not source.is_relative_to(original):
                        continue
                    dest = root / source.relative_to(original)
                    if source.is_file():
                        dest.parent.mkdir(parents=True, exist_ok=True)
                        shutil.copy2(source, dest)
                    elif source.is_dir():
                        dest.mkdir(parents=True, exist_ok=True)
            copied = [root / path.relative_to(original) for path in sources]
            with patch.object(feature_map, "ROOT", root), patch.object(feature_map, "FEATURES", root / "features"), \
                    patch.object(feature_map, "INVENTORY", root / "features/inventory.csv"), \
                    patch.object(feature_map, "source_files", return_value=copied):
                with contextlib.redirect_stdout(io.StringIO()):
                    feature_map.validate()
                changed = root / "source/GameInterface/Services/Towns/Commands/TownDebugCommand.cs"
                saved = changed.read_bytes()
                changed.write_bytes(saved + b"\r\n// isolated stale-source witness\r\n")
                with self.assertRaisesRegex(SystemExit, "inventory.csv differs"):
                    feature_map.validate()
                changed.write_bytes(saved)
                leaves_path = root / "features/behavior-leaves.csv"
                saved_leaves = leaves_path.read_bytes()
                with leaves_path.open(encoding="utf-8", newline="") as stream:
                    reader = csv.DictReader(stream)
                    fields = reader.fieldnames
                    leaves = list(reader)

                def write_leaves():
                    with leaves_path.open("w", encoding="utf-8", newline="") as stream:
                        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
                        writer.writeheader()
                        writer.writerows(leaves)

                original_parent = leaves[0]["parent"]
                leaves[0]["parent"] = "absent.group"
                write_leaves()
                with self.assertRaisesRegex(SystemExit, "unknown parent"):
                    feature_map.validate()
                leaves[0]["parent"] = original_parent
                backed = next(row for row in leaves if row["kind"] == "perk-effect")
                original_evidence = backed["evidence"]
                backed["evidence"] = original_evidence.split("@", 1)[0] + "@1-99999999"
                write_leaves()
                with self.assertRaisesRegex(SystemExit, "invalid evidence range"):
                    feature_map.validate()
                leaves_path.write_bytes(saved_leaves)
                sources_path = root / "features/baseline/behavior-sources.json"
                saved_sources = sources_path.read_bytes()
                manifest = json.loads(saved_sources)
                installed = next(row for row in manifest["sources"].values() if row["provider"] != "repository")
                installed["sha256"] = "0" * 64
                sources_path.write_text(json.dumps(manifest), encoding="utf-8")
                with self.assertRaisesRegex(SystemExit, "differs from installed baseline"):
                    feature_map.validate()
                sources_path.write_bytes(saved_sources)
                recipe = root / "features/recipes/selection.md"
                recipe.write_text(recipe.read_text(encoding="utf-8") + "\n[missing](absent-evidence.json)\n", encoding="utf-8")
                with self.assertRaisesRegex(SystemExit, "broken/outside link absent-evidence.json"):
                    feature_map.validate()
                self.assertEqual((original / changed.relative_to(root)).read_bytes(), saved)


if __name__ == "__main__":
    unittest.main()
