"""Read-only capture of model-line inputs; all outputs stay in this experiment."""
from pathlib import Path
import subprocess, shutil, hashlib, json
HERE = Path(__file__).resolve().parent
MODEL = HERE.parents[2] / 'ChimeraEvolution-modeli'
RAW = HERE.parents[2] / '_refs-local' / 'эксперименты' / 'oboroten-telo-st2-2026-10-21'
OUT = HERE / 'inputs'
OUT.mkdir(exist_ok=True)
prior_path = HERE / 'inputs-manifest.json'
prior = json.loads(prior_path.read_text(encoding='utf-8')) if prior_path.exists() else {}
def capture(src, dst):
    digest = hashlib.sha256(src.read_bytes()).hexdigest()
    if dst.name in prior and digest != prior[dst.name]['sha256']:
        raise RuntimeError(f'Input changed since experiment: {src}. Recover recorded revision instead of replacing evidence.')
    shutil.copyfile(src, dst)
    return {'source': str(src), 'sha256': digest}
files = {}
for rel in ['Assets/_Chimera/Models/chelovek_mesh.fbx', 'Docs/models/rig/chelovek-skelet.json',
            'Docs/models/handoff/klyuchi/chelovek-volk-poza.json', 'Docs/models/handoff/klyuchi/chelovek-volk-sustavy.json',
            'Tools/Blender/proba/podgonka_v_poze.py', 'Tools/Blender/proba/vesa_gladit.py']:
    src = MODEL / rel
    dst = OUT / src.name
    files[dst.name] = capture(src, dst)
for name in ['cel_A_norm.obj', 'orientiry-celi.json']:
    src = RAW / name
    files[name] = capture(src, OUT / name)
(HERE / 'inputs-manifest.json').write_text(json.dumps(files, ensure_ascii=False, indent=2), encoding='utf-8')
print('Captured', len(files), 'inputs')
