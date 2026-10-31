"""Explicit adapter over the frozen c13 stand; does not edit its source file."""
from pathlib import Path
import sys
root=Path(__file__).resolve().parent
sys.path.insert(0,str(root))
source=root.parent/'podzharyj-c10-2026-10-31'/'stend_k2.py'
text=source.read_text(encoding='utf-8')
old="math.radians(-sign*angle),4,'Y'"
assert text.count(old)==1
text=text.replace(old,"math.radians(-sign*angle if opt.get('pose_axis','Y')=='Y' else angle),4,opt.get('pose_axis','Y')")
needle='dg = bpy.context.evaluated_depsgraph_get()'
assert text.count(needle)==1
insert="""
if opt.get('support'):
    from support_motion import apply_support
    cfg=json.load(open(opt['support'],encoding='utf-8'))['selected']
    err=apply_support(arm,float(opt.get('raised_angle','70')),opt.get('pose_axis','Y'),cfg)
    otchet['support_matrix_error']=err
    otchet['support_config']=cfg
    assert err<1e-5, ('Support bone cannot reproduce requested transform',err)
"""
text=text.replace(needle,insert+'\n'+needle)
exec(compile(text,str(source),'exec'),globals())
