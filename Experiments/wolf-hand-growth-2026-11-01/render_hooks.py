import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
levels=[0,.18,.35,.60];rows=[]
for variant,filename,lean,region in [('source','original.blend',False,True),('hooks','hooks.blend',False,True)]:
    bpy.ops.wm.open_mainfile(filepath=str(root/filename))
    organs=[o for o in bpy.data.objects if o.type=='MESH' and '@' in o.name]
    claw=next(o for o in organs if o.name=='Руки@Волк')
    dup=claw.copy();dup.data=claw.data.copy();bpy.context.scene.collection.objects.link(dup)
    bm=bmesh.new();bm.from_mesh(dup.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(dup.data);bm.free()
    sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=700;sc.render.resolution_y=900;sc.render.resolution_percentage=100
    sh=sc.display.shading;sh.light='STUDIO';sh.color_type='OBJECT';sh.single_color=(.72,.68,.62);sh.show_backface_culling=True;sh.show_object_outline=False
    world=bpy.data.worlds.new('hand-white');world.color=(1,1,1);sc.world=world
    cam=bpy.data.objects.new('hand-camera',bpy.data.cameras.new('hand-camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.40
    angle=math.radians(35);direction=Vector((math.sin(angle),-math.cos(angle),0));center=Vector((-.32,-.01,.875));cam.location=center+direction*10;cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
    for i,share in enumerate(levels):
        growth=min(1,share/.65);area=min(1,share/.85) if region else 0
        for o in bpy.data.objects:
            if o.type!='MESH':o.hide_render=True;continue
            o.hide_render=(o.name.startswith('@') or ('@' in o.name and o not in (claw,dup)))
            if o in (claw,dup):o.hide_render=share==0
            if o.data.shape_keys:
                for k in o.data.shape_keys.key_blocks:
                    k.value=1 if lean and k.name=='Поджарый' else area if k.name=='Волк·Руки' else growth if k.name=='рост' and o in (claw,dup) else 0
            o.color=(.20,.18,.16,1) if o in (claw,dup) else (.72,.68,.62,1)
            for f in o.data.polygons:f.use_smooth=False
        sc.render.filepath=str(root/f'{variant}-{i}.png');bpy.ops.render.render(write_still=True)
        rows.append(dict(variant=variant,share=share,growth=growth,region=area,global_stage=0))
(root/'states-hooks.json').write_text(json.dumps(dict(rows=rows,note='Weights reproduce source BodyMesh formulas for an isolated parametric share below WeakAt; no inventory/budget or full game simulation.'),ensure_ascii=False,indent=2),encoding='utf-8')
