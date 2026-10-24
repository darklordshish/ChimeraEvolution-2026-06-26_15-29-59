"""Reuse the already checked identical-light renderer without changing the previous experiment."""
from pathlib import Path
import sys
root=Path(__file__).resolve().parent; prev=root.parent/'morph-weights-2026-10-23'
sys.path.insert(0,str(prev))
code=(prev/'render_comparison.py').read_text(encoding='utf-8')
code=code.replace("paths={'current':'inputs/source.blend','fit':'refit.blend','changed':'weights.blend','corrected':'corrected.blend'}",
                  "paths={'input':'input.blend','old':'old.blend','planes':'planes.blend'}")
code=code.replace("pose=read_pose(root/('inputs/chelovek-volk-poza.json' if variant=='current' else 'pose.json'))",
                  "pose=read_pose(root.parent/'morph-weights-2026-10-23/pose.json')")
variant=sys.argv[sys.argv.index('--')+1]
if '--mask' in sys.argv:
    code=code.replace("shade.color_type='OBJECT'", "shade.light='FLAT'; shade.color_type='SINGLE'; shade.single_color=(0,0,0)")
    code=code.replace("out=root/'renders'", "out=root/'masks'")
sys.argv=['render','--',str(root),variant]
exec(compile(code,str(prev/'render_comparison.py'),'exec'))
