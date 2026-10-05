"""Focused checks for feature discovery and stale/broken evidence rejection. No game processes."""

import contextlib
import io
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
                recipe = root / "features/recipes/selection.md"
                recipe.write_text(recipe.read_text(encoding="utf-8") + "\n[missing](absent-evidence.json)\n", encoding="utf-8")
                with self.assertRaisesRegex(SystemExit, "broken/outside link absent-evidence.json"):
                    feature_map.validate()
                self.assertEqual((original / changed.relative_to(root)).read_bytes(), saved)


if __name__ == "__main__":
    unittest.main()
