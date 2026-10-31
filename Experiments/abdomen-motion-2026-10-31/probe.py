"""Diagnostic torso poses on actual rig, geometry unchanged, paired comparison."""
import bpy,json,math,numpy as np
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(__file__).resolve().parent
poses=[('rest',0,'X'),('flex25',25,'X'),('extend15',-15,'X'),('twist30',30,'Z')]
reports=[]
for variant,source in [('original',root.parent/'shoulder-local-2026-10-31/candidate.blend'),('abs',root.parent/'abdomen-lowpoly-2026-10-31/candidate.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(source))
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');aw=arm.matrix_world
    rest={b.name:(aw@b.matrix_local).copy() for b in arm.data.bones}
    objects=[]
    for o in bpy.data.objects:
        o.hide_render=o.type not in ('MESH','ARMATURE') or '@' in o.name
        if o.type=='MESH' and '@' not in o.name:
            if o.data.shape_keys:
                for k in o.data.shape_keys.key_blocks:k.value=1 if k.name=='Поджарый' else 0
            for f in o.data.polygons:f.use_smooth=False
            objects.append(o)
    sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=800;sc.render.resolution_y=800;sc.render.resolution_percentage=100
    sh=sc.display.shading;sh.light='STUDIO';sh.color_type='SINGLE';sh.single_color=(.72,.68,.62);sh.show_backface_culling=True;sh.show_object_outline=False
    world=bpy.data.worlds.new('motion-white');world.color=(1,1,1);sc.world=world
    cam=bpy.data.objects.new('motion-camera',bpy.data.cameras.new('motion-camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.78
    r=math.radians(35);direction=Vector((math.sin(r),-math.cos(r),0));center=Vector((0,-.015,1.25))
    cam.location=center+direction*10;cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
    samples={}
    for o in objects:
        gn={g.index:g.name for g in o.vertex_groups}
        coords=np.array([list(o.matrix_world@p.co) for p in o.data.shape_keys.key_blocks['Поджарый'].data])
        fs=np.array([[f.vertices[0],f.vertices[j],f.vertices[j+1]] for f in o.data.polygons for j in range(1,len(f.vertices)-1)])
        dom=[max(((g.weight,gn[g.group]) for g in v.groups if gn[g.group] in rest),default=(0,'хребет'))[1] for v in o.data.vertices]
        samples[o.name]=(coords,fs,dom)
    for label,angle,axis in poses:
        for b in arm.pose.bones:b.matrix_basis=Matrix.Identity(4)
        bpy.context.view_layer.update()
        # Two explicit torso joints; diagnostic pose, not a proposed game animation.
        pivot=aw@arm.data.bones['живот'].head_local
        d1=Matrix.Translation(pivot)@Matrix.Rotation(math.radians(angle*.5),4,axis)@Matrix.Translation(-pivot)
        pivot2=aw@arm.data.bones['грудная клетка'].head_local
        d2=d1@Matrix.Translation(pivot2)@Matrix.Rotation(math.radians(angle*.5),4,axis)@Matrix.Translation(-pivot2)
        arm.pose.bones['живот'].matrix=aw.inverted()@d1@rest['живот'];bpy.context.view_layer.update()
        for n in ('грудная клетка','шея','лопатка','лопатка.L','опора_подмышки','опора_подмышки.L'):
            arm.pose.bones[n].matrix=aw.inverted()@d2@rest[n];bpy.context.view_layer.update()
        skin={n:np.array((aw@arm.pose.bones[n].matrix)@rest[n].inverted())[:3,:3] for n in rest}
        dg=bpy.context.evaluated_depsgraph_get();seams={};maxgap=0.;folds=compressed=0;minratio=10.
        for o in objects:
            ev=o.evaluated_get(dg);mesh=ev.to_mesh();P=np.array([list(ev.matrix_world@v.co) for v in mesh.vertices]);X,F,dom=samples[o.name]
            center_tri=X[F].mean(axis=1);mask=(abs(center_tri[:,0])<.11)&(center_tri[:,1]<-.07)&(center_tri[:,2]>1.115)&(center_tri[:,2]<1.325)
            n0=np.cross(X[F[:,1]]-X[F[:,0]],X[F[:,2]]-X[F[:,0]])
            n1=np.cross(P[F[:,1]]-P[F[:,0]],P[F[:,2]]-P[F[:,0]])
            refs=np.array([skin[dom[f[0]]]@n for f,n in zip(F,n0)])
            folds+=int(((np.einsum('fi,fi->f',n1,refs)<-1e-10)&mask).sum())
            ratios=np.linalg.norm(n1,axis=1)/np.maximum(np.linalg.norm(n0,axis=1),1e-12)
            compressed+=int(((ratios<.25)&mask).sum())
            if mask.any():minratio=min(minratio,float(ratios[mask].min()))
            for v in o.data.vertices:
                key=tuple(round(t,4) for t in o.matrix_world@v.co)
                if key in seams:maxgap=max(maxgap,float(np.linalg.norm(P[v.index]-seams[key])))
                else:seams[key]=P[v.index]
            ev.to_mesh_clear()
        reports.append(dict(variant=variant,pose=label,fold_flags=folds,compressed_faces=compressed,min_patch_area_ratio=minratio,seam_max_mm=maxgap*1000))
        sc.render.filepath=str(root/f'{variant}_{label}.png');bpy.ops.render.render(write_still=True)
(root/'motion.json').write_text(json.dumps(dict(poses=[dict(name=n,angle=a,axis=ax) for n,a,ax in poses],rows=reports,note='Synthetic two-joint torso poses, PSD off. Dominant-bone orientation flags, not self-intersection detection or anatomical scoring. Different triangulations; counts not directly equivalent.'),ensure_ascii=False,indent=2),encoding='utf-8')
print('MOTION',reports)
