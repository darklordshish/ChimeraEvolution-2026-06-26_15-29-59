# -*- coding: utf-8 -*-
"""КАДРЫ 3D-ОБРАЗЦА — GLB из multiview-генератора в ортокамере Blender: анфас, профиль, спина, 3/4 сзади и спереди.
Запуск:  blender -b --python obrazec_kadry.py -- образец.glb папка
"""
import bpy, sys, math
from mathutils import Vector
glb, out = sys.argv[sys.argv.index('--')+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
import bmesh
pts=[o.matrix_world @ v.co for o in obs for v in o.data.vertices]
mn=Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)))
mx=Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
c=(mn+mx)/2; h=mx.z-mn.z
sc=bpy.context.scene
sc.render.engine='BLENDER_WORKBENCH'
sc.display.shading.light='STUDIO'; sc.display.shading.color_type='SINGLE'
sc.display.shading.single_color=(0.75,0.75,0.78)
sc.world=bpy.data.worlds.new('w'); sc.world.color=(0.02,0.022,0.027)
sc.render.resolution_x=500; sc.render.resolution_y=900
cam=bpy.data.cameras.new('c'); cam.type='ORTHO'; cam.ortho_scale=h*1.08
co=bpy.data.objects.new('c',cam); sc.collection.objects.link(co); sc.camera=co
# gltf import: Y-up → Blender Z-up; front of sample = -Y in Blender (glTF +Z)
views={'1-анфас':0,'2-профиль':90,'3-спина':180,'4-три-четверти-сзади':145,'5-три-четверти-спереди':-35}
for n,yaw in views.items():
    a=math.radians(yaw); d=Vector((math.sin(a),-math.cos(a),0.12 if 'три' in n else 0)).normalized()
    co.location=c+d*10; co.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=f'{out}/{n}.png'; bpy.ops.render.render(write_still=True)
