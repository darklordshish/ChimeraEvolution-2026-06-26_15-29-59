"""КАДР ПОСАДКИ РОГА ЛОСЯ НА МЕСТО ЧЕЛОВЕКА (30.10): повторяет формулу `PartPieces` (кадр шва куска → кадр места, масштаб — супремум
отношений эллипсов, половина куска по знаку x центра шва, вторая сторона зеркалом по X) и снимает анфас, профиль, 3/4, сзади.

  blender -b --factory-startup -P rog_posadka_kadr.py -- место.json префикс_кадров    (из корня репо)
"""
import bpy, json, math, sys
from mathutils import Vector, Matrix
a=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='Assets/_Chimera/Models/los_mesh.fbx')
R=[o for o in bpy.data.objects if o.name=='Рога'][0]
dg=bpy.context.evaluated_depsgraph_get(); Re=R.evaluated_get(dg); me=Re.to_mesh()
RV=[Re.matrix_world@v.co for v in me.vertices]; RF=[list(p.vertices) for p in me.polygons]
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath='Assets/_Chimera/Models/chelovek_mesh.fbx')
for o in list(bpy.data.objects):
    if o.type=='MESH' and '@' in o.name: bpy.data.objects.remove(o)
U=lambda q: Vector((-q.x,q.z,-q.y)); Bk=lambda u: Vector((-u.x,-u.z,u.y))
d=json.load(open('Docs/models/handoff/parts/los-roga.json',encoding='utf-8'))['seam']; c=json.load(open(a[0],encoding='utf-8'))['seam']
V3=lambda l: Vector(l)
def frame(ax,up):
    f=ax.normalized(); r=up.cross(f).normalized(); u=f.cross(r)
    return Matrix((r,u,f)).transposed()
k=max(c['ellipse_m'][0]/d['ellipse_m'][0], c['ellipse_m'][1]/d['ellipse_m'][1])
for side in (1,-1):
    M=lambda v: Vector((-v.x,v.y,v.z)) if side<0 else v
    dC,dA,dU,cC,cA,cU=[M(V3(x)) for x in (d['center_m'],d['axis_out'],d['up'],c['center_m'],c['axis_out'],c['up'])]
    rot=frame(cA,cU)@frame(dA,dU).inverted()
    keep=[i for i,v in enumerate(RV) if U(v).x*V3(d['center_m']).x>0]; idx={i:j for j,i in enumerate(keep)}
    P=[]
    for i in keep:
        p=M(U(RV[i])); P.append(Bk(cC+rot@(p-dC)*k))
    F=[[idx[x] for x in f] for f in RF if all(x in idx for x in f)]
    m=bpy.data.meshes.new('r'); m.from_pydata(P,[],F); ob=bpy.data.objects.new('рог%d'%side,m); bpy.context.scene.collection.objects.link(ob)
print('k',round(k,3))
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sc.render.resolution_x=600; sc.render.resolution_y=600; sc.world=bpy.data.worlds.new('w')
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera=cam; cam.data.type='ORTHO'; cam.data.ortho_scale=1.2
for nm,ang in [('front',0),('profile',90),('34',35),('back',180)]:
    r=math.radians(ang); dd=Vector((math.sin(r),-math.cos(r),0))
    cam.location=Vector((0,0,1.6))+dd*4; cam.rotation_euler=(math.pi/2,0,r)
    sc.render.filepath=a[1]+'_'+nm+'.png'; bpy.ops.render.render(write_still=True)
