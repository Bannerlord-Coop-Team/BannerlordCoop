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
        with tempfile.TemporaryDirectory(prefix="bannerlord-map-check-") as temporary:
            root = Path(temporary)
            shutil.copytree(original / "features", root / "features")
            shutil.copytree(original / ".agents", root / ".agents")
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
            manifest = json.loads((root / "features/baseline/behavior-sources.json").read_text(encoding="utf-8"))
            for source in manifest["sources"].values():
                if source["provider"] == "repository":
                    target = root / source["assembly"]
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(original / source["assembly"], target)
            with patch.object(feature_map, "ROOT", root), patch.object(feature_map, "FEATURES", root / "features"):
                with contextlib.redirect_stdout(io.StringIO()):
                    feature_map.validate()
                command = root / "source/GameInterface/Services/Towns/Commands/TownDebugCommand.cs"
                saved_command = command.read_bytes()
                with patch.object(feature_map, "source_files", return_value=[command]):
                    before = [row for row in feature_map.inventory_rows() if row["kind"] == "command"]
                    command.write_bytes(saved_command + b"\r\n// isolated inventory freshness witness\r\n")
                    after = [row for row in feature_map.inventory_rows() if row["kind"] == "command"]
                self.assertTrue(before)
                self.assertEqual([row["id"] for row in before], [row["id"] for row in after])
                self.assertNotEqual(before[0]["sha256"], after[0]["sha256"])
                command.write_bytes(saved_command)
                changed = root / manifest["sources"]["coop.trade"]["assembly"]
                saved = changed.read_bytes()
                changed.write_bytes(saved + b"\r\n// isolated stale-source witness\r\n")
                with self.assertRaisesRegex(SystemExit, "repository behavior source changed"):
                    feature_map.validate()
                changed.write_bytes(saved)
                cases_path = root / "features/source-cases.csv"
                saved_cases = cases_path.read_bytes()
                with cases_path.open(encoding="utf-8", newline="") as stream:
                    reader = csv.DictReader(stream)
                    fields = reader.fieldnames
                    cases = list(reader)

                def write_cases():
                    with cases_path.open("w", encoding="utf-8", newline="") as stream:
                        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
                        writer.writeheader()
                        writer.writerows(cases)

                original_parent = cases[0]["parent"]
                cases[0]["parent"] = "absent.group"
                write_cases()
                with self.assertRaisesRegex(SystemExit, "unknown parent"):
                    feature_map.validate()
                cases[0]["parent"] = original_parent
                backed = cases[0]
                original_evidence = backed["evidence"]
                backed["evidence"] = original_evidence.split("@", 1)[0] + "@1-99999999"
                write_cases()
                with self.assertRaisesRegex(SystemExit, "invalid evidence range"):
                    feature_map.validate()
                cases_path.write_bytes(saved_cases)
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
