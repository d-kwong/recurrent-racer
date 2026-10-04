"""Encode actual compact captures or independently recorded ghost replay frames."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import statistics
import subprocess
import tempfile


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('frames', type=Path);p.add_argument('--output', type=Path, required=True)
    p.add_argument('--kind', choices=['driving', 'activations', 'ghosts'], required=True)
    p.add_argument('--manifest', type=Path, required=True, help='Audited capture or replay metadata')
    p.add_argument('--start', type=float, required=True);p.add_argument('--duration', type=float, default=8)
    p.add_argument('--gif-width', type=int, default=960);p.add_argument('--ffmpeg', required=True)
    a = p.parse_args(); column = 'replay_seconds' if a.kind == 'ghosts' else 'episode_seconds'
    evidence=json.loads(a.manifest.read_text())
    rows = list(csv.DictReader((a.frames/'frames.csv').open()))
    excluded_endpoint_frames=0
    if a.kind=='ghosts':
        # Keep the first endpoint image, not the frozen frames after renderer completion.
        endpoint=next((i for i in range(1,len(rows)) if float(rows[i][column])==float(rows[i-1][column])),len(rows))
        excluded_endpoint_frames=len(rows)-endpoint;rows=rows[:endpoint]
    reset = next((i for i in range(1,len(rows)) if float(rows[i][column]) < float(rows[i-1][column])),len(rows))
    discarded = len(rows)-reset;rows=rows[:reset]
    times = [float(row[column]) for row in rows]
    positive = [b-x for x,b in zip(times,times[1:]) if b>x]
    if not positive:
        raise ValueError('No recorded advancing simulation/replay time')
    cadence=statistics.median(positive)
    durations=[b-x if b>x else cadence for x,b in zip(times,times[1:])]+[cadence]
    if a.duration<=0 or a.start<times[0] or a.start+a.duration>times[-1]:
        raise ValueError('Excerpt outside recorded timeline')
    paths=[(a.frames/f"frame-{int(row['frame']):05d}.png").resolve() for row in rows]
    if any(not path.exists() for path in paths):raise ValueError('Missing recorded frame')
    a.output.parent.mkdir(parents=True,exist_ok=True)
    mp4=a.output.with_suffix('.mp4');gif=a.output.with_suffix('.gif')
    if mp4.exists() or gif.exists():raise FileExistsError('Preserve existing media; select a new output')
    with tempfile.TemporaryDirectory() as temp:
        folder=Path(temp);lines=[]
        for i,(source,duration) in enumerate(zip(paths,durations)):
            name=f'frame-{i:05d}.png';(folder/name).symlink_to(source)
            lines.extend([f"file '{name}'",f'duration {duration:.9f}'])
        lines.append(f"file 'frame-{len(paths)-1:05d}.png'")
        concat=folder/'concat.txt';concat.write_text('\n'.join(lines)+'\n')
        subprocess.run([a.ffmpeg,'-v','error','-n','-f','concat','-safe','0','-i',str(concat),'-fps_mode','vfr','-c:v','libx264','-crf','22','-pix_fmt','yuv420p','-movflags','+faststart',str(mp4)],check=True)
        subprocess.run([a.ffmpeg,'-v','error','-n','-ss',str(a.start-times[0]),'-t',str(a.duration),'-i',str(mp4),'-filter_complex',f'fps=12,scale={a.gif_width}:-1:flags=lanczos,split[x][y];[x]palettegen[p];[y][p]paletteuse','-loop','0',str(gif)],check=True)
    # Manifests are evidence supplied by the sole execution owner, not inferred successes.
    def public(value):
        if isinstance(value,dict):return {key:public(item) for key,item in value.items()}
        if isinstance(value,list):return [public(item) for item in value]
        if isinstance(value,str) and value.startswith('/'):return Path(value).name
        return value
    result={'kind':a.kind,'evidence':public(evidence),'source_manifest_sha256':hashlib.sha256(a.manifest.read_bytes()).hexdigest(),
            'recorded_interval_seconds':[times[0],times[-1]],'excerpt_interval_seconds':[a.start,a.start+a.duration],
            'initial_visual_acquisition_seconds':times[0],'excluded_reset_frames':discarded,
            'excluded_post_endpoint_frames':excluded_endpoint_frames,
            'playback_speed': '1x recorded simulated/replay time',
            'median_advancing_frame_gap_seconds': cadence,
            'maximum_advancing_frame_gap_seconds': max(positive),
            'advancing_gaps_over_twice_median_count': sum(gap > 2*cadence for gap in positive),
            'frozen_timestamp_frames':sum(b==x for x,b in zip(times,times[1:])),
            'extra_encoded_frozen_hold_seconds':sum(b==x for x,b in zip(times,times[1:]))*cadence,
            'timing_column':column,'frame_count':len(rows),
            'terminal_hold_frame_count':sum(row.get('final_hold','').lower()=='true' for row in rows),
            'asset_sha256':{file.name:hashlib.sha256(file.read_bytes()).hexdigest() for file in [mp4,gif]},
            'note':'Ghosts replay separate policy runs on one track without interactions; stopped cars hold their actual terminal poses.' if a.kind=='ghosts' else 'Contiguous recorded first episode; gaps and reset trimming are explicit.'}
    for file in [mp4,gif]:subprocess.run([a.ffmpeg,'-v','error','-i',str(file),'-f','null','-'],check=True)
    result['decode_check']='full MP4 and GIF decode passed'
    a.output.with_suffix('.media.json').write_text(json.dumps(result,indent=2)+'\n')


if __name__=='__main__':main()
