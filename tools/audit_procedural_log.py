"""Extract compact provenance and termination evidence from Unity structured logs."""
import argparse
import json
from pathlib import Path


def records(path, marker):
    result = []
    for line in path.read_text(errors='replace').splitlines():
        if marker in line:
            result.append(json.loads(line.split(marker, 1)[1].strip()))
    return result


def audit(path):
    tracks = records(path, 'RACING_TRACK: ')
    episodes = records(path, 'RACING_EPISODE: ')
    identities = {}
    for record in tracks:
        key = json.dumps([record['generatorVersion'], record['seed'], record['parameters']], sort_keys=True)
        previous = identities.setdefault(key, record['geometrySha256'])
        if previous != record['geometrySha256']:
            raise ValueError('Same seed/parameters produced different geometry')
    compact_tracks = {record['geometrySha256']: {key: record[key] for key in
                      ('generatorVersion', 'seed', 'parameters', 'acceptedAttempt', 'geometrySha256', 'length', 'gateDistances',
                       'spawnDistance', 'finishDistance', 'taskDistance', 'spawnPosition', 'finishPosition') if key in record}
                      for record in tracks}
    task_fields = ('geometrySha256', 'taskSha256', 'seed', 'sequenceIndex', 'spawnSeed', 'spawnTurnIndex',
                   'spawnDistance', 'spawnApproach', 'taskDistance', 'actualSpawnPosition', 'spawnForward',
                   'activeGateIndices', 'skippedGateCount', 'finishDistance', 'actualFinishGatePosition', 'finishForward')
    tasks = {record.get('taskSha256', record['geometrySha256']):
             {key:record[key] for key in task_fields if key in record} for record in tracks}
    for episode in episodes:
        if episode['geometrySha256'] not in compact_tracks:
            raise ValueError('Terminal references unrecorded geometry')
        if episode.get('taskSha256', episode['geometrySha256']) not in tasks:
            raise ValueError('Terminal references unrecorded spawn task')
        if episode['interrupted'] != (episode['reason'] == 'Timeout'):
            raise ValueError('Task termination / interruption mismatch')
        if episode['finishDistance'] <= 0:
            raise ValueError('Nonpositive credited task distance')
        episode['normalized_progress'] = 1. if episode['reason'] == 'Finish' else max(0., min(1., episode['travelMetres']/episode['finishDistance']))
    return dict(track_resets=len(tracks), unique_tracks=list(compact_tracks.values()), unique_tasks=list(tasks.values()), episodes=episodes,
                completion_count=sum(e['reason']=='Finish' for e in episodes),
                complete_episode_count=len(episodes),
                note='Structured terminal telemetry; excludes partial episodes at interruption. Geometry arrays retained in ignored Unity log.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('log', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = audit(args.log)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(f"{report['track_resets']} resets, {len(report['unique_tracks'])} geometries, "
          f"{report['completion_count']}/{report['complete_episode_count']} complete terminal episodes finished")


if __name__ == '__main__':
    main()
