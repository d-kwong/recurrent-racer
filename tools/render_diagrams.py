"""Editable SVG diagrams. Optional PNG export requires cairosvg and a Cairo library."""
from pathlib import Path
import html,math
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'docs/media'
BG='#0d1726';PANEL='#17263a';TEXT='#eef5fa';MUTED='#a8bdcf';TEAL='#46dfce';AMBER='#ffbe65';EDGE='#31465c'
class SVG:
 def __init__(self,w,h,title,subtitle):
  self.w=w;self.h=h;self.parts=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" role="img"><title>{html.escape(title)}</title><defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10" fill="{TEAL}"/></marker></defs><rect width="100%" height="100%" rx="18" fill="{BG}"/>']
  self.text(35,43,title,27,TEXT,True);self.text(35,75,subtitle,17,MUTED)
 def text(self,x,y,s,size=19,color=TEXT,bold=False,anchor='start'):
  self.parts.append(f'<text x="{x}" y="{y}" fill="{color}" font-size="{size}" font-family="Arial, Helvetica, sans-serif" font-weight="{700 if bold else 400}" text-anchor="{anchor}">{html.escape(s)}</text>')
 def box(self,x,y,w,h,title,lines):
  self.parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="12" fill="{PANEL}" stroke="{EDGE}"/>');self.text(x+20,y+32,title,20,TEAL,True)
  for i,s in enumerate(lines):self.text(x+20,y+63+25*i,s,18,MUTED)
 def path(self,d,color=TEAL,arrow=False,dash=None,width=2.5):
  self.parts.append(f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{width}"'+(' marker-end="url(#arrow)"' if arrow else '')+(f' stroke-dasharray="{dash}"' if dash else '')+'/>')
 def save(self,name):
  p=OUT/(name+'.svg');p.write_text(''.join(self.parts)+'</svg>\n')
  try:
   import cairosvg
   cairosvg.svg2png(url=str(p),write_to=str(OUT/(name+'.png')))
  except ImportError:pass

a=SVG(1120,610,'From physics to policy','One car · 15 numerical observations · two continuous controls · no camera input')
a.text(60,119,'UNITY / C#',16,AMBER,True);a.text(415,119,'PYTHON / PYTORCH',16,AMBER,True)
a.path('M365 100 V565',EDGE,dash='6 6',width=2)
a.box(40,145,285,140,'Racing environment',['50 Hz vehicle physics','Road-edge ray queries','Progress reward + boundaries'])
a.box(415,145,290,140,'Unity transport',['11 ray distances; 4 motion fields','Reward and final observation','ML-Agents: 10 Hz decisions'])
a.box(760,145,320,140,'SAC actor',['15 → 64 → 64 → mean / std','Train: tanh(Gaussian sample)','Evaluate: tanh(mean)'])
a.path('M325 208 H415',arrow=True);a.path('M705 208 H760',arrow=True)
a.box(760,335,320,110,'Bounded action',['Steering / signed throttle','Each command in [−1, +1]'])
a.box(40,335,285,110,'Vehicle control',['Filtered steering; bicycle yaw','Positive throttle / negative brake'])
a.path('M920 285 V335',arrow=True);a.path('M760 386 H325',arrow=True);a.text(420,370,'Actions held for 5 physics ticks',16,MUTED)
a.path('M180 335 V285',arrow=True)
a.box(415,485,290,95,'Uniform replay',['(s, a, r, s′, task_terminated)'])
a.box(760,485,320,95,'SAC updates',['Twin Q, actor, learned α, targets'])
a.path('M560 285 V335 H380 V532 H415',arrow=True);a.text(383,471,'Training transitions only',15,MUTED)
a.path('M705 532 H760',arrow=True);a.path('M1087 532 H1101 V205 H1080',arrow=True,dash='5 5')
a.save('architecture')

a=SVG(1120,670,'The driving interface','Ray geometry schematic · directions show the full 40 m range; live clips show actual hits')
origin=(270,385);length=205
for i,angle in enumerate([-90,-60,-45,-30,-15,0,15,30,45,60,90]):
 x=origin[0]+math.sin(math.radians(angle))*length;y=origin[1]-math.cos(math.radians(angle))*length
 a.path(f'M{origin[0]} {origin[1]} L{x:.2f} {y:.2f}',TEAL,dash='6 4',width=2)
 a.text(x+math.sin(math.radians(angle))*22,y-math.cos(math.radians(angle))*20,f'{angle}°',16,MUTED,anchor='middle')
a.parts.append(f'<rect x="254" y="365" width="32" height="80" rx="10" fill="{BG}" stroke="{AMBER}" stroke-width="2"/><circle cx="270" cy="385" r="6" fill="{AMBER}"/>')
a.text(270,122,'LOCAL +Z / FORWARD',16,AMBER,True,anchor='middle');a.text(270,495,'Physics pose, not interpolated render pose',17,MUTED,anchor='middle')
a.text(270,524,'Origin = body pose + local (0, 0.15, 0) m',17,MUTED,anchor='middle');a.text(270,554,'Layer 8 road edges · triggers included',17,MUTED,anchor='middle')
a.box(575,115,510,240,'15 inputs · single unstacked float32 vector',['0–10: ray hit distance / 40 m; no hit = 1','11: local forward velocity / 48 m/s','12: local lateral velocity / 20 m/s','13: angular velocity about Y / 4 rad/s','14: applied steering angle / 22°','Motion values clipped to [−1, +1]'])
a.box(575,385,510,182,'2 actions · tanh normalized',['0: steering; −1 left / +1 right','1: longitudinal; + accelerates / − brakes','Throttle = max(0, a[1]); brake = max(0, −a[1])','Steering limit = 22° / (1 + 0.025 × |speed|)'])
a.text(35,610,'50 Hz physics · 10 Hz decisions · steering command slews at 1.5 normalized units/s',18,TEAL)
a.text(35,644,'No images, position, waypoints, gate index, track map or recurrent state are supplied to the actor.',17,MUTED)
a.save('interface')

a=SVG(1120,835,'How the implemented SAC learns','Uniform replay · twin critics · differentiable bounded actor · automatic temperature tuning')
a.box(35,110,500,130,'01  Collect experience',['Drive Unity with sampled bounded actions','Store aligned reward / next state / task ending','Replay capacity: 250,000; warm-up: 2,000'])
a.box(585,110,500,130,'02  Sample replay',['Minibatch: 256; a′ ~ current actor at s′','Every 4 transitions: 4 update cycles','Evaluation rollouts never enter replay'])
a.path('M535 175 H585',arrow=True)
a.box(585,280,500,150,'03  Update both critics',['y = 10r + 0.995(1−d) Vsoft(s′)','Vsoft = min Qtarget(s′,a′) − α log π(a′|s′)','Minimize MSE(Q1, y) + MSE(Q2, y)','Detached target; d = true task termination'])
# Replace long target line with a smaller formula that stays inside the panel.
a.parts[-3]=a.parts[-3].replace('font-size="18"','font-size="17"')
a.path('M835 240 V280',arrow=True)
a.box(35,280,500,150,'04  Improve the actor',['Minimize mean[α log π(a|s) − min Q(s,a)]','Reparameterized sample + stable tanh Jacobian','Freeze Q weights; keep gradients through action','Actor: 64/64 tanh; each critic: 128/128 ReLU'])
a.path('M585 355 H535',arrow=True)
a.box(35,475,500,135,'05  Learn the entropy coefficient',['α = exp(log α), initialized at 0.02','Target entropy: −2','Lα = −mean[log α × (log π + target entropy)]'])
a.path('M285 430 V475',arrow=True)
a.box(585,475,500,135,'06  Move target critics slowly',['Qtarget ← 0.995 Qtarget + 0.005 Q','Adam: 0.0003; gradient norm cap: 10','No separate value network'])
a.path('M535 543 H585',arrow=True);a.path('M1085 543 H1100 V264 H835',arrow=True,dash='5 5')
a.box(35,655,1050,130,'Boundary handling and evaluation',['True task terminations stop bootstrap; 120 s time-limit interruptions use the actual final observation.','Every ~10,000 training transitions, evaluate tanh(mean) from the original start between episodes.','Selection: finish fraction first → shorter estimated finished-lap time → raw episode return.'])
a.save('sac-training')
print('Rendered architecture, interface and SAC diagrams.')
