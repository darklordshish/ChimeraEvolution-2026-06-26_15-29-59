"""Изолированная художественная проба Шкуры. Не меняет игровые ассеты.
Волк: разметка исходных граней; змея: кожа, нарезанная локальным
вариантом алгоритма Модельного, без тонких полос кромок.
"""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Matrix, Vector
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P/'snake-coarse.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
BASE=['хребет','шея','Сердце','Руки']
species=['Змея','Волк']
manifest={'source_sha256':hashlib.sha256((P/'source.fbx').read_bytes()).hexdigest(),'variants':{}}

def candidates(vid):
    return [bpy.data.objects[n] for n in BASE] if vid=='Волк' else [o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Шкура.тон') and o.name.endswith('@Змея')]

def classify(vid,c,n,src):
    if vid=='Волк':
        # Единая тёмная полоса вдоль позвоночника, расширяющаяся к загривку.
        # Перелом по бокам следует массе, на передней стороне тона спокойнее.
        neck=max(0.,min(1.,(c.z-1.30)/.25))
        width=.045+.045*neck
        if c.z>1.09 and n.y>.18 and abs(c.x)<width: return 2
        if c.z>1.05 and n.y>-.12: return 1
        return 0
    # Ромбические ячейки одного тона, без рельефной накладки и обводки.
    if not (1.06<c.z<1.66 and n.y>.2): return 0
    step=.065
    u=(math.sin(.62)*c.x+math.cos(.62)*c.z)/step
    v=(-math.sin(.62)*c.x+math.cos(.62)*c.z)/step
    # Целая ячейка одного тона: внутренние треугольники не дробят рисунок.
    row=math.floor(u)+math.floor(v)
    return 2 if row%2==0 else 0

def build(vid):
    srcs=candidates(vid)
    sources_by_name={o.name:o for o in srcs}
    # У каждого нового объекта ровно те же координаты кожи во всех ключах.
    names=list(dict.fromkeys(k.name for o in srcs if o.data.shape_keys for k in o.data.shape_keys.key_blocks))
    assert names[0]=='Basis'
    buckets=[dict(V=[],F=[],W=[],K={n:[] for n in names},ix={}) for _ in range(3)]
    tones=(1.0,.87,.74) if vid=='Змея' else (1.0,.84,.69)
    expected=0
    for o in srcs:
        me=o.data; me.calc_loop_triangles(); expected+=len(me.loop_triangles)
        M=o.matrix_world.copy(); gn={g.index:g.name for g in o.vertex_groups}
        kb=me.shape_keys.key_blocks if me.shape_keys else None
        for f in me.polygons:
            pts=[M@(kb[0].data[i].co if kb else me.vertices[i].co) for i in f.vertices]
            c=sum(pts,Vector())/len(pts); n=(pts[1]-pts[0]).cross(pts[2]-pts[0]).normalized()
            t=classify(vid,c,n,o); B=buckets[t]; face=[]
            for i in f.vertices:
                ix=(o.name,i)
                if ix not in B['ix']:
                    B['ix'][ix]=len(B['V']); B['V'].append(pts[list(f.vertices).index(i)])
                    B['W'].append({gn[g.group]:g.weight for g in me.vertices[i].groups})
                    for kn in names:
                        k=kb.get(kn) if kb else None
                        B['K'][kn].append(M@(k.data[i].co if k else (kb[0].data[i].co if kb else me.vertices[i].co)))
                face.append(B['ix'][ix])
            B['F'].append(face)
    report={'triangles_source':expected,'objects':[],'max_key_error_m':0.,'max_weight_error':0.,'max_bone_influences':0}
    bones={b.name for b in arm.data.bones}
    for t,B in enumerate(buckets):
        name='Шкура.тон%d@%s'%(t+1,vid)
        # Сохраняем исходные тоновые объекты для сравнения, не экспортируем их.
        old=bpy.data.objects.get(name)
        if old: old.name='ИСХОДНИК_'+name
        if not B['F']: continue
        me=bpy.data.meshes.new(name); me.from_pydata(B['V'],[],B['F']); me.update()
        ob=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(ob)
        ob.parent=arm; ob.matrix_world=Matrix.Identity(4)
        mod=ob.modifiers.new('Armature','ARMATURE'); mod.object=arm
        for i,w in enumerate(B['W']):
            report['max_bone_influences']=max(report['max_bone_influences'],sum(1 for n,x in w.items() if n in bones and x>1e-6))
            for n,x in w.items():
                g=ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n); g.add([i],x,'REPLACE')
        for kn in names:
            k=ob.shape_key_add(name=kn)
            for i,co in enumerate(B['K'][kn]): k.data[i].co=co
        for p in me.polygons: p.use_smooth=False
        mat=bpy.data.materials.new(name+'_множитель'); mat.diffuse_color=(tones[t],)*3+(1.,)
        mat.use_nodes=True; mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=mat.diffuse_color
        me.materials.append(mat)
        # Проверка фактически записанного Blender-меша, каждого ключа и веса.
        for (srcname,si),di in B['ix'].items():
            so=sources_by_name[srcname]
            sk=so.data.shape_keys.key_blocks if so.data.shape_keys else None
            for kn in names:
                key=sk.get(kn) if sk else None
                target=so.matrix_world@(key.data[si].co if key else (sk[0].data[si].co if sk else so.data.vertices[si].co))
                err=(ob.matrix_world@ob.data.shape_keys.key_blocks[kn].data[di].co-target).length
                report['max_key_error_m']=max(report['max_key_error_m'],err)
            actual={ob.vertex_groups[g.group].name:g.weight for g in me.vertices[di].groups}
            for n,x in B['W'][di].items(): report['max_weight_error']=max(report['max_weight_error'],abs(actual.get(n,0)-x))
        me.calc_loop_triangles(); report['objects'].append({'name':name,'triangles':len(me.loop_triangles),'keys':names,'tone':tones[t]})
    report['triangles_result']=sum(o['triangles'] for o in report['objects'])
    assert report['triangles_source']==report['triangles_result']
    assert report['max_key_error_m']<1e-6 and report['max_weight_error']<1e-6
    assert report['max_bone_influences']<=4
    return report

# Сначала змея: её источники переименовываются по мере построения.
for vid in species: manifest['variants'][vid]=build(vid)
(P/'verification.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'candidate.blend'))
print(json.dumps(manifest,ensure_ascii=False))
