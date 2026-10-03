"""Plot one audited full-route quality report; no training-return substitutions."""
import argparse
import json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('report', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = json.loads(args.report.read_text())
    routes = report['routes']
    if any(r['spawnTurnIndex'] != -1 for r in routes):
        raise ValueError('Quality figure requires original full-route starts')
    plt.rcParams.update({'font.family':'DejaVu Sans','font.size':10,'svg.fonttype':'none',
        'figure.facecolor':'#111b21','axes.facecolor':'#111b21','savefig.facecolor':'#111b21',
        'text.color':'#edf3f5','axes.labelcolor':'#a7b6be','xtick.color':'#a7b6be',
        'ytick.color':'#a7b6be','axes.edgecolor':'#3b4a52'})
    fig, (status, time) = plt.subplots(1,2,figsize=(11,4.5),gridspec_kw={'width_ratios':[1,2]})
    counts = [report['clean_count'],report['finish_count']-report['clean_count'],len(routes)-report['finish_count']]
    status.bar(['Clean finish','Other finish','Nonfinish'],counts,color=['#72d49a','#efbd70','#f17f7a'])
    status.set(ylabel='Original routes',ylim=(0,len(routes)+1),title='Full-route outcomes')
    for i,count in enumerate(counts):status.text(i,count+.3,str(count),ha='center')
    x = np.arange(len(routes))
    finished = [r['reason']=='Finish' for r in routes]
    colors = ['#72d49a' if r['quality']['cleanFinish'] else '#efbd70' for r in routes]
    time.scatter([i for i,f in enumerate(finished) if f],[r['simulatedSeconds'] for r in routes if r['reason']=='Finish'],c=[colors[i] for i,f in enumerate(finished) if f],s=27)
    time.set(xlabel='Route index (requested seed order)',ylabel='Actual finished simulated time (s)',title='Finished routes only',xticks=range(0,len(routes),max(1,len(routes)//8)))
    for axis in (status,time):
        axis.spines[['top','right']].set_visible(False)
        axis.grid(axis='y',alpha=.15);axis.set_axisbelow(True)
    fig.suptitle(f"{report['finish_count']} / {len(routes)} full routes finished",x=.065,y=.97,ha='left',fontsize=19,fontweight='bold')
    fig.text(.065,.89,'One frozen actor · original starts · physics-tick timing · failures have no invented finish time',color='#a7b6be')
    fig.subplots_adjust(left=.065,right=.97,top=.75,bottom=.26,wspace=.35)
    fig.text(.065,.12,'Clean: reverse ≤0.5 s · unfinished stall ≤2 s after launch grace · off asphalt ≤5% of driving time',fontsize=9,color='#a7b6be')
    fig.text(.065,.06,'Policy '+str(report['policy_sha256'])[:12]+' · source '+str(args.report.relative_to(ROOT) if args.report.is_absolute() else args.report),fontsize=9,color='#a7b6be')
    args.output.parent.mkdir(parents=True,exist_ok=True)
    for ext in ('png','svg'):fig.savefig(args.output.with_suffix('.'+ext),dpi=160)
    plt.close(fig)


if __name__ == '__main__':main()
