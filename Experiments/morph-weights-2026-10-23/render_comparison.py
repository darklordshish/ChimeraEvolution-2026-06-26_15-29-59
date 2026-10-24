"""Diagnostic Blender renders, same camera/material for every variant. No image generation."""
import sys, json
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from check_roundtrip import read_pose,skin_matrices,vertex_matrix,worldpoint

a=sys.argv[sys.argv.index('--')+1:]; root=Path(a[0]).resolve(); variant=a[1]
paths={'current':'inputs/source.blend','fit':'refit.blend','changed':'weights.blend','corrected':'corrected.blend'}
bpy.ops.wm.open_mainfile(filepath=str(root/paths[variant]))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
pose=read_pose(root/('inputs/chelovek-volk-poza.json' if variant=='current' else 'pose.json'))
skin=skin_matrices(arm,pose)
for o in list(bpy.data.objects):
    if o.type!='MESH' or '@' in o.name or o.name in ('голова','Чутьё'):
        if o!=arm: bpy.data.objects.remove(o,do_unlink=True)
        continue
    if not o.data.shape_keys: continue
    key=o.data.shape_keys.key_blocks['Волк']
    coords=[]
    for v in o.data.vertices:
        m=vertex_matrix(o,v,skin)
        coords.append((m@np.r_[worldpoint(o,key.data[v.index].co),1])[:3].tolist())
    faces=[list(p.vertices) for p in o.data.polygons]
    mesh=bpy.data.meshes.new(o.name+'_baked'); mesh.from_pydata(coords,[],faces); mesh.update()
    oldname=o.name; bpy.data.objects.remove(o,do_unlink=True)
    ob=bpy.data.objects.new(oldname,mesh); bpy.context.scene.collection.objects.link(ob)
    ob.color=(0.48,0.50,0.53,1)
bpy.data.objects.remove(arm,do_unlink=True)
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'
sc.render.resolution_x=900; sc.render.resolution_y=1000; sc.render.resolution_percentage=100
shade=sc.display.shading; shade.light='STUDIO'; shade.studiolight_rotate_z=0.35
shade.color_type='OBJECT'; shade.show_shadows=False; shade.show_cavity=False; shade.show_specular_highlight=False
shade.background_type='WORLD'; sc.world=bpy.data.worlds.new('DiagnosticWorld'); sc.world.color=(0.82,0.82,0.82)
sc.view_settings.view_transform='Standard'
cam=bpy.data.objects.new('DiagnosticCamera',bpy.data.cameras.new('DiagnosticCamera'))
sc.collection.objects.link(cam); sc.camera=cam; cam.data.type='ORTHO'
out=root/'renders'; out.mkdir(exist_ok=True)
for view,d in [('front',(0,-1,0)),('side',(-1,0,0)),('34',(-0.7,-0.7,0))]:
    d=Vector(d).normalized(); cam.data.ortho_scale=2.15
    cam.location=Vector((0,0,0.94))+10*d; cam.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    sc.render.filepath=str(out/f'{variant}_{view}.png'); bpy.ops.render.render(write_still=True)
    cam.data.ortho_scale=0.95
    cam.location=Vector((0,-0.04,1.28))+10*d
    sc.render.filepath=str(out/f'{variant}_{view}_torso.png'); bpy.ops.render.render(write_still=True)
