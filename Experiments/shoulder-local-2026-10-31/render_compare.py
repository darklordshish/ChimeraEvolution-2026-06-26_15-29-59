"""Actual Blender meshes, fixed camera/light/scale; no generated imagery."""
import bpy,sys,math,json
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(__file__).resolve().parent
sys.path.insert(0,str(root.parent/'shoulder-support-2026-10-31'))
from support_motion import apply_support
cfg=json.loads((root.parent/'shoulder-support-2026-10-31/helper-benchmark.json').read_text(encoding='utf-8'))['selected']
for variant,path in [('before',root.parent/'shoulder-support-2026-10-31/support.blend'),('after',root/'candidate.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(path))
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');aw=arm.matrix_world
    rest={b.name:aw@b.matrix_local for b in arm.data.bones}
    for o in bpy.data.objects:
        if o.type not in ('MESH','ARMATURE'):o.hide_render=True
        if o.type=='MESH':
            o.hide_render='@' in o.name
            if o.data.shape_keys:
                for k in o.data.shape_keys.key_blocks:k.value=1 if k.name=='Поджарый' else 0
            for p in o.data.polygons:p.use_smooth=False
    sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=800;sc.render.resolution_y=800;sc.render.resolution_percentage=100
    sh=sc.display.shading;sh.light='STUDIO';sh.color_type='SINGLE';sh.single_color=(.72,.68,.62);sh.show_backface_culling=True;sh.show_object_outline=False
    w=bpy.data.worlds.new('comparison-white');w.color=(1,1,1);sc.world=w
    cam=bpy.data.objects.new('comparison-camera',bpy.data.cameras.new('comparison-camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.94
    for label,angle,axis in [('rest',0,'Y'),('raise45',45,'Y'),('raise90',90,'Y'),('forward60',-60,'X')]:
        for b in arm.pose.bones:b.matrix_basis=Matrix.Identity(4)
        bpy.context.view_layer.update()
        for suffix in ('','.L'):
            pb=arm.pose.bones['плечо'+suffix];h=aw@pb.head;side=1 if h.x>0 else -1
            d=Matrix.Translation(h)@Matrix.Rotation(math.radians(-side*angle if axis=='Y' else angle),4,axis)@Matrix.Translation(-h)
            pb.matrix=aw.inverted()@d@rest[pb.name];bpy.context.view_layer.update()
        apply_support(arm,angle,axis,cfg)
        for view,deg in [('front',-25),('back',205)]:
            r=math.radians(deg);direction=Vector((math.sin(r),-math.cos(r),0))
            center=Vector((.08,0,1.35));cam.location=center+direction*10;cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
            sc.render.filepath=str(root/f'{variant}_{label}_{view}.png');bpy.ops.render.render(write_still=True)
