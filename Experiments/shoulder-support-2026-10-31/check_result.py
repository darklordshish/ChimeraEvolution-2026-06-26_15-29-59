"""Acceptance check written before the shoulder motion experiment.

Neutral geometry must stay unchanged. Fold reduction cannot be purchased by
making another measured pose worse. Same definition of folds for all variants.
"""
import sys,json
from pathlib import Path
d=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
if 'skladka_v_poze' in d:
    assert d['skladka_v_poze']==0, 'Baseline still folds: '+str(d['skladka_v_poze'])
else:
    assert d['neutral_max_delta_m']<1e-7
    b=d['baseline']; c=d['candidate']
    assert len(b)==len(c) and b
    assert sum(x['folds'] for x in c)<=.75*sum(x['folds'] for x in b), 'Less than 25% fold reduction'
    assert all(y['folds']<=x['folds'] for x,y in zip(b,c)), 'A pose regressed'
    assert all(y['seam_max_mm']<1 for y in c), 'A shared seam opened'
print('PASS: neutral geometry, measured poses and seams')
