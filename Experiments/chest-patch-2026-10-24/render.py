import bpy,sys
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent;variant=sys.argv[sys.argv.index('--')+1]
bpy.ops.wm.open_mainfile(filepath=str(root/(variant+'.blend')))
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=900;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
s=sc.display.shading;s.light='STUDIO';s.studiolight_rotate_z=.35;s.color_type='OBJECT';s.show_shadows=False;s.show_cavity=False;s.show_specular_highlight=False
s.background_type='WORLD';sc.world=bpy.data.worlds.new('World');sc.world.color=(.82,.82,.82);sc.view_settings.view_transform='Standard'
cam=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO'
out=root/'renders';out.mkdir(exist_ok=True)
for name,d in [('front',(0,-1,0)),('34',(-.7,-.7,0)),('side',(-1,0,0))]:
    d=Vector(d).normalized();cam.data.ortho_scale=.95;cam.location=Vector((0,-.04,1.28))+d*10;cam.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    sc.render.filepath=str(out/f'{variant}_{name}.png');bpy.ops.render.render(write_still=True)
