"""Reproduce the controlled experiment. Requires prepared inputs from manifest."""
import subprocess,sys,hashlib,json
from pathlib import Path
root=Path(__file__).resolve().parent
blender='C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
manifest=json.loads((root/'inputs-manifest.json').read_text(encoding='utf-8'))
for name,m in manifest.items():
    assert hashlib.sha256((root/'inputs'/name).read_bytes()).hexdigest()==m['sha256'], name
def bl(script,*args,expected=0):
    log=root/(Path(script).stem+'-run.log')
    with log.open('w',encoding='utf-8') as f:
        p=subprocess.run([blender,'-b','--factory-startup','--python-exit-code','1','-P',str(root/script),'--',*map(str,args)],stdout=f,stderr=subprocess.STDOUT)
    if p.returncode!=expected: raise RuntimeError(f'{script}: exit {p.returncode}, expected {expected}; see {log}')
bl('inspect_input.py',root)
bl('inputs/podgonka_v_poze.py',root/'inputs/template.blend',root/'inputs/cel_A_norm.obj',root/'inputs/orientiry-celi.json',
   root/'inputs/chelovek-skelet.json',root/'refit.blend','--vstrechnye','1',
   '--vstr_maxd','0.1,предплечье:0.05,предплечье.L:0.05,бедро:0.03,бедро.L:0.03',
   '--zhestkost','30,15,8,4,2','--poza',root/'pose.json','--sustavy',root/'joints.json','--poza_obj',root/'posed.obj')
bl('check_roundtrip.py',root,'refit.blend','baseline.json')
bl('inputs/vesa_gladit.py',root/'refit.blend',root/'weights.blend','1.04','1.40','0.02','4')
bl('check_roundtrip.py',root,'weights.blend','changed.json',expected=1)
assert json.loads((root/'changed.json').read_text())['max_m']>0.01
bl('rebind_key.py',root)
bl('check_roundtrip.py',root,'corrected.blend','corrected.json')
bl('check_blender.py',root)
for variant in ['current','fit','changed','corrected']: bl('render_comparison.py',root,variant)
subprocess.run([sys.executable,str(root/'make_sheet.py')],check=True)
print('PASS: reproduced failing control, corrected key, independent Blender check, and comparison sheets')
