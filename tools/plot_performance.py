"""Plot real deterministic evaluation data; no training returns or invented error bars."""
from pathlib import Path
import csv,json
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
ROOT=Path(__file__).resolve().parents[1]
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':12,'svg.fonttype':'none','text.color':'#eef5fa','axes.labelcolor':'#a8bdcf','xtick.color':'#a8bdcf','ytick.color':'#a8bdcf','axes.edgecolor':'#31465c','axes.facecolor':'#17263a','figure.facecolor':'#0d1726','savefig.facecolor':'#0d1726'})
rows=[]
for run in ['lap-v1','lap-v2']:
 with (ROOT/f'results/selection/{run}-evaluations.csv').open() as f:rows.extend(csv.DictReader(f))
steps=np.array([int(r['step']) for r in rows])/1000
rate=np.array([float(r['reward_inferred_finish_fraction']) for r in rows])
times=np.array([float(r['mean_finished_seconds']) if r['mean_finished_seconds'] else np.nan for r in rows])
fig=plt.figure(figsize=(12,6.8));gs=fig.add_gridspec(2,2,width_ratios=[1.55,1],height_ratios=[1,1],hspace=.8,wspace=.3)
a=fig.add_subplot(gs[:,0]);a.plot(steps,times,'o-',color='#46dfce',lw=2,ms=5,label='Finished original-start evaluations')
a.scatter([49.232],[20],color='#ffbe65',s=110,zorder=4);a.annotate('Selected\n49,232 transitions / 20.0 s',(49.232,20),xytext=(47,21.65),color='#ffbe65',fontsize=11,arrowprops={'arrowstyle':'->','color':'#ffbe65'})
a.set(xlabel='Cumulative SAC training transitions (thousands)',ylabel='Estimated finished-lap time (s)',ylim=(19.5,23),xlim=(-3,114));a.grid(alpha=.12);a.set_title('Checkpoint comparisons',loc='left',color='#eef5fa',fontweight='bold',pad=15)
a.text(.03,.06,'No lap at 0 or 10,034 transitions.\nThree repeated starts per checkpoint;\nall finished times within each batch are identical.',transform=a.transAxes,color='#a8bdcf',fontsize=10)
b=fig.add_subplot(gs[0,1]);b.step(steps,rate*100,where='post',color='#46dfce');b.scatter(steps,rate*100,color='#46dfce',s=24);b.set(ylim=(-8,108),yticks=[0,50,100],ylabel='Finish proxy (%)');b.grid(alpha=.12);b.set_title('Original-start completion',loc='left',color='#eef5fa',fontweight='bold');b.set_xticks([0,50,100]);b.set_xlabel('Training transitions (thousands)')
c=fig.add_subplot(gs[1,1]);records=json.loads((ROOT/'results/verification/curated-headless-result.json').read_text());labels=['Original','10 m','15 m','20 m'];data=[records[k]['mean_finished_seconds'] for k in ['0','10','15','20']];c.bar(labels,data,color=['#ffbe65','#46dfce','#46dfce','#46dfce'],width=.6);c.set(ylim=(0,26),ylabel='Time to finish (s)');c.set_title('Selected actor · fresh evaluation',loc='left',color='#eef5fa',fontweight='bold')
for i,t in enumerate(data):c.text(i,t+.4,f'{t:.1f} s\n3/3',ha='center',fontsize=10,color='#eef5fa')
fig.suptitle('A selected policy that completes the fixed track',x=.06,y=.975,ha='left',fontsize=20,fontweight='bold')
fig.text(.06,.907,'Deterministic tanh(mean) · Unity seed 7 · n=3 repeated episodes per start · 120 s cap',color='#a8bdcf',fontsize=11)
fig.subplots_adjust(left=.075,right=.965,top=.8,bottom=.19)
fig.text(.06,.075,'Time = decisions × 0.1 s (terminal interval can overestimate by ≈0.08 s). Finish is inferred from terminal reward.',color='#a8bdcf',fontsize=10)
fig.text(.06,.04,'Approach starts shorten the route; they are not held-out tracks. No independent training-seed or generalization result.',color='#a8bdcf',fontsize=10)
for ext in ['svg','png']:fig.savefig(ROOT/f'docs/media/performance.{ext}',dpi=150)
print('Performance plot generated from preserved CSV/JSON evaluation records.')
