"""Encode actual Unity PNG frames, preserving their recorded wall-time spacing.

Usage: python tools/encode_media.py runs/hero-frames hero --ffmpeg /path/to/ffmpeg
Requires FFmpeg separately; no codec binary is redistributed in this repository.
"""
import argparse,csv,hashlib,json,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('frames',type=Path);p.add_argument('name');p.add_argument('--ffmpeg',default='ffmpeg');p.add_argument('--start',type=float,default=0);p.add_argument('--duration',type=float,default=0);args=p.parse_args()
rows=list(csv.DictReader((args.frames/'frames.csv').open()));first=float(rows[0]['realtime_seconds'])
rows=[r for r in rows if float(r['realtime_seconds'])-first>=args.start and (not args.duration or float(r['realtime_seconds'])-first<args.start+args.duration)]
media=ROOT/'docs/media';media.mkdir(exist_ok=True)
with tempfile.TemporaryDirectory() as td:
 listing=Path(td)/'frames.txt'
 lines=[]
 for i,r in enumerate(rows):
  frame=(args.frames/f"frame-{int(r['frame']):05d}.png").resolve()
  # FFmpeg concat quoting; arbitrary paths are never interpolated into a shell.
  escaped=str(frame).replace("'", "'\\''")
  lines.append("file '"+escaped+"'")
  dt=float(rows[i+1]['realtime_seconds'])-float(r['realtime_seconds']) if i+1<len(rows) else .05
  lines.append(f'duration {dt:.8f}')
 lines.append(lines[-2]);listing.write_text('\n'.join(lines)+'\n')
 base=[args.ffmpeg,'-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',str(listing)]
 subprocess.run(base+['-vf','fps=30','-c:v','libx264','-preset','slow','-crf','22','-pix_fmt','yuv420p','-movflags','+faststart',str(media/(args.name+'.mp4'))],check=True)
 subprocess.run(base+['-filter_complex','fps=10,scale=720:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff:reserve_transparent=0[p];[b][p]paletteuse=dither=bayer:bayer_scale=3','-loop','0',str(media/(args.name+'.gif'))],check=True)
 subprocess.run(base+['-frames:v','1',str(media/(args.name+'-poster.png'))],check=True)
manifest=dict(source='Unity ScreenCapture.CaptureScreenshotAsTexture; actual selected-policy rollout',policy='policies/selected.pt',policy_sha256=hashlib.sha256((ROOT/'policies/selected.pt').read_bytes()).hexdigest(),training_transitions=49232,seed=7,frame_count=len(rows),source_size=[1280,720],source_wall_duration_seconds=float(rows[-1]['realtime_seconds'])-float(rows[0]['realtime_seconds'])+.05,simulation_interval_seconds=[float(rows[0]['episode_seconds']),float(rows[-1]['episode_seconds'])],editing='Contiguous frames only; original timestamp spacing resampled to MP4 30 fps and GIF 10 fps; no retiming.',camera='Overhead ray view' if args.name=='perception' else 'Chase camera: 35 degrees down, 22 m, 48 degree FOV',rays=args.name=='perception',files={})
for ext in ['mp4','gif','png']:
 path=media/(args.name+('-poster' if ext=='png' else '')+'.'+ext);manifest['files'][path.name]={'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
(media/(args.name+'-capture.json')).write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({'name':args.name,'frames':len(rows),'duration':manifest['source_wall_duration_seconds'],'files':{k:v['bytes'] for k,v in manifest['files'].items()}}))
