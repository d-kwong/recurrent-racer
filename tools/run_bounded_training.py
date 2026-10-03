"""Enforce a session wall ceiling and save orderly on SIGINT, including evaluation."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import signal
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--wall-seconds', type=float, required=True)
    parser.add_argument('--log', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command or args.wall_seconds <= 0:
        parser.error('Positive wall budget and command required')
    if args.log.exists() or args.report.exists():
        parser.error('Use new log and report paths')
    args.log.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic(); began = datetime.now(timezone.utc).isoformat(); watchdog = False
    with args.log.open('w') as log:
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT,
                                   start_new_session=True, env=dict(os.environ, PYTHONDONTWRITEBYTECODE='1'))
        try:
            code = process.wait(timeout=args.wall_seconds)
        except subprocess.TimeoutExpired:
            watchdog = True; process.send_signal(signal.SIGINT)
            try:
                code = process.wait(timeout=30)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGTERM); code = process.wait(timeout=10)
    report = dict(started_utc=began, wall_budget_seconds=args.wall_seconds,
                  actual_wall_seconds=time.monotonic()-started, watchdog_signaled=watchdog, exit_code=code,
                  note='Budget includes Python initialization, connection, training and evaluation. At ceiling SIGINT stops further collection/evaluation and permits up to30s orderly checkpoint/Unity cleanup.')
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))
    raise SystemExit(code)


if __name__ == '__main__': main()
