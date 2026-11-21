import bpy,runpy,sys
from pathlib import Path
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P/'candidate.blend'))
for o in list(bpy.data.objects):
    if o.name.startswith('ИСХОДНИК_'): bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'export.blend'))
sys.argv=['eksport_k2-source.py','--',str(P/'export.blend'),str(P/'candidate.fbx')]
runpy.run_path(str(P/'eksport_k2-source.py'),run_name='__main__')
