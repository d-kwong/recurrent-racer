"""Freeze compact development routes from geometry alone, before policy outcomes."""
import argparse
import hashlib
import json
from pathlib import Path
from preselect_quality_captures import select


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('geometry', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Frozen selection already exists')
    data = json.loads(args.geometry.read_text())
    records = data if isinstance(data, list) else data['tracks']
    if sorted(record['seed'] for record in records) != list(range(50000, 50008)):
        parser.error('Expected all eight compact development geometry records')
    if any(record['generatorVersion'] != 'compact-circuit-v1' for record in records):
        parser.error('Expected compact-circuit-v1 geometry')
    result = select(records)
    result['geometry_export_sha256'] = hashlib.sha256(args.geometry.read_bytes()).hexdigest()
    result['comparison_seed'] = result['selected_seeds'][0]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(result['selected_seeds'])


if __name__ == '__main__':
    main()
