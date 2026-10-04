"""Render only the new concise learning diagram; preserve historical figures."""
from pathlib import Path
import html

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/media/compact'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="1120" height="560" viewBox="0 0 1120 560" role="img"><title>How the implemented SAC learns</title><defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0 0L10 5L0 10" fill="#46dfce"/></marker></defs><rect width="1120" height="560" rx="18" fill="#0d1726"/>']

    def text(x, y, value, size=17, color='#a8bdcf', bold=False):
        parts.append(f'<text x="{x}" y="{y}" fill="{color}" font-size="{size}" font-family="Arial, Helvetica, sans-serif" font-weight="{700 if bold else 400}">{html.escape(value)}</text>')

    def box(x, y, title, lines):
        parts.append(f'<rect x="{x}" y="{y}" width="325" height="125" rx="12" fill="#17263a" stroke="#31465c"/>')
        text(x+18, y+30, title, 19, '#46dfce', True)
        for i, line in enumerate(lines):
            text(x+18, y+59+24*i, line)

    def arrow(path, dash=False):
        parts.append(f'<path d="{path}" fill="none" stroke="#46dfce" stroke-width="2.5" marker-end="url(#arrow)"'+(' stroke-dasharray="5 5"' if dash else '')+'/>')

    text(35, 43, 'How the implemented SAC learns', 27, '#eef5fa', True)
    text(35, 75, 'Stochastic experience → replayed updates → deterministic evaluation', 17)
    box(35, 110, '01  Collect stochastic actions', ['15 observations → Gaussian actor', 'tanh(sample) → steering / throttle', 'Unity supplies reward and next state'])
    box(398, 110, '02  Align and replay transitions', ['Store (s, a, r, s′, terminated)', 'Uniform replay → minibatches', 'Evaluation never enters replay'])
    box(761, 110, '03  Update twin critics', ['Soft target uses min target Q', 'True termination stops bootstrap', 'Time limits retain final state'])
    arrow('M360 172H398'); arrow('M723 172H761')
    box(761, 275, '04  Improve actor + entropy', ['Actor minimizes α log π − min Q', 'Gradients flow through tanh actions', 'Learn α toward target entropy −2'])
    box(398, 275, '05  Move targets slowly', ['Target Q ← 0.995 target + 0.005 Q', 'Repeat updates on replayed data', 'No separate value network'])
    box(35, 275, '06  Evaluate and select', ['Drive with tanh(mean); no noise', 'Original full routes, held apart', 'Completion → clean rate → time'])
    arrow('M923 235V275'); arrow('M761 337H723'); arrow('M398 337H360')
    arrow('M560 275V253H560V235', True)
    text(35, 449, 'Training updates weights. Evaluation measures a frozen policy and does not teach it.', 19, '#eef5fa')
    text(35, 484, 'The actor is feedforward: 15 → 64 tanh → 64 tanh → 2 means; standard deviation is used during training.', 17)
    text(35, 521, 'Compact quality selection compares time only across the same all-finished tasks; the untouched test never selects a policy.', 16)
    path = OUT / 'learning.svg'
    path.write_text(''.join(parts) + '</svg>\n')
    try:
        import cairosvg
        cairosvg.svg2png(url=str(path), write_to=str(path.with_suffix('.png')))
    except ImportError:
        pass


if __name__ == '__main__':
    main()
