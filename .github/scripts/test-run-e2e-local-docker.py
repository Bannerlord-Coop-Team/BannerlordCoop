#!/usr/bin/env python3
"""Check local shard scheduling and failure propagation without starting Docker."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class LocalDockerTests(unittest.TestCase):
    def run_launcher(self, shards=None, failed_shard=None):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            docker = root / "docker"
            docker.write_text("""#!/usr/bin/env python3
import json, os, sys, time
args = sys.argv[1:]
def record(event):
    with open(os.environ['DOCKER_EVENTS'], 'a') as output:
        output.write(json.dumps([event, args]) + '\\n')
record('start')
if args[0] == 'run':
    deadline = time.monotonic() + 3
    while time.monotonic() < deadline:
        with open(os.environ['DOCKER_EVENTS']) as events:
            starts = sum(json.loads(line)[0] == 'start' and json.loads(line)[1][0] == 'run' for line in events)
        if starts == int(os.environ.get('E2E_SHARDS', '4')):
            break
        time.sleep(.01)
record('end')
sys.exit(1 if args[0] == 'run' and args[args.index('--name') + 1].endswith('-' + os.environ.get('FAILED_SHARD', 'none')) else 0)
""")
            docker.chmod(0o755)
            env = dict(os.environ, PATH=f"{root}:{os.environ['PATH']}",
                       DOCKER_EVENTS=str(root / "events"))
            env.pop("E2E_SHARDS", None)
            env.pop("FAILED_SHARD", None)
            if shards is not None:
                env["E2E_SHARDS"] = shards
            if failed_shard is not None:
                env["FAILED_SHARD"] = failed_shard
            result = subprocess.run(
                ["bash", str(Path(__file__).with_name("run-e2e-local-docker.sh"))],
                env=env, capture_output=True, text=True)
            events = root / "events"
            return result, [json.loads(line) for line in events.read_text().splitlines()] if events.exists() else []

    def test_default_four_shards_start_before_any_finishes(self):
        result, events = self.run_launcher()
        self.assertEqual(0, result.returncode, result.stderr)
        runs = [(event, args) for event, args in events if args[0] == "run"]
        self.assertEqual(["start"] * 4 + ["end"] * 4, [event for event, _ in runs])
        for index in range(4):
            self.assertTrue(any(f"run-e2e-shard.sh {index} 4" in args[-1] for _, args in runs))

    def test_override_and_failure_still_run_remaining_shards_and_cleanup(self):
        result, events = self.run_launcher("2", "0")
        self.assertEqual(1, result.returncode)
        runs = [args for event, args in events if event == "start" and args[0] == "run"]
        self.assertEqual(2, len(runs))
        self.assertTrue(any("run-e2e-shard.sh 1 2" in args[-1] for args in runs))
        self.assertTrue(any(args[:2] == ["image", "rm"] for _, args in events))

    def test_invalid_shard_count_starts_no_containers(self):
        result, events = self.run_launcher("0")
        self.assertEqual(1, result.returncode)
        self.assertFalse(any(args[0] in ("create", "start", "run") for _, args in events))


if __name__ == "__main__":
    unittest.main()
