"""РЕЛЬЕФ БАЗЫ ПОВЕРХ ПОДГОНКИ (30.10, поджарый человек). Цель Hunyuan гладкая, и подгонка к ней стирает рельеф шаблона К2 (грудь,
лопатки, грани мышц); паспорт листа требует рельеф. Ключ `ключ` пересобирается так: база + СГЛАЖЕННОЕ смещение подгонки (Таубин,
`шагов` проходов) — крупная форма от цели, мелкая — от базы. Общие вершины швов склеиваются по расстоянию 1 мм (сглаживаются одной
точкой). Где сглаженное смещение больше R0..R1 м, рельеф гасится плавно до нуля (там форма ушла далеко, и деталь базы торчала бы);
объекты из списка `без_рельефа` берутся из подгонки как есть (шея: её полоса у средней линии в К2 утоплена под трапецией «Рук» и
после подгонки выступала зубцом на затылке).

  blender -b вход.blend --factory-startup -P relef_bazy.py -- выход.blend ключ шагов R0,R1 [без_рельефа]   (пример: Поджарый 10 0.02,0.05 шея)
"""
import bpy, sys, numpy as np
a=sys.argv[sys.argv.index('--')+1:]; out, KEY, n_it = a[0], a[1], int(a[2]); R0,R1=(float(x) for x in (a[3] if len(a)>3 else '9,10').split(','))
OBJ=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.data.shape_keys and KEY in o.data.shape_keys.key_blocks]
from mathutils import kdtree, Vector
vm={}; B=[]; K=[]; KS=[]
tot=sum(len(o.data.vertices) for o in OBJ); kd=kdtree.KDTree(tot); j=0
for o in OBJ:
    for i,v in enumerate(o.data.shape_keys.key_blocks[0].data): kd.insert(o.matrix_world@v.co, j); j+=1
kd.balance(); flat=[]
for o in OBJ:
    kb=o.data.shape_keys.key_blocks
    for i,v in enumerate(kb[0].data): flat.append((o,i,o.matrix_world@v.co))
rep=[None]*tot
for j,(o,i,p) in enumerate(flat):
    if rep[j] is not None: continue
    u=len(B); B.append(tuple(o.data.shape_keys.key_blocks[0].data[i].co)); K.append(tuple(o.data.shape_keys.key_blocks[KEY].data[i].co))
    for (_,jj,_) in kd.find_range(p,0.001):
        if rep[jj] is None: rep[jj]=u
for j,(o,i,p) in enumerate(flat): vm[(o.name,i)]=rep[j]
B=np.array(B); K=np.array(K); N=len(B); nb=[set() for _ in range(N)]
for o in OBJ:
    for e in o.data.edges:
        x,y=vm[(o.name,e.vertices[0])],vm[(o.name,e.vertices[1])]; nb[x].add(y); nb[y].add(x)
NB=[list(s) for s in nb]; D=K-B
# смещение склеенной точки — среднее по её копиям
acc=np.zeros_like(D); cnt=np.zeros(len(B))
for (o,i,p) in flat: u=vm[(o.name,i)]; acc[u]+=np.array(o.data.shape_keys.key_blocks[KEY].data[i].co)-np.array(o.data.shape_keys.key_blocks[0].data[i].co); cnt[u]+=1
D=acc/cnt[:,None]; D0=D.copy()
for _ in range(n_it):
    for lam in (0.5,-0.53):
        L=np.array([D[n].mean(0)-D[k] if n else np.zeros(3) for k,n in enumerate(NB)]); D=D+lam*L
# рельеф базы гасится там, где форма ушла далеко (|сглаженное смещение| от R0 до R1 м): ключ там — сама подгонка
AL=np.clip((R1-np.linalg.norm(D,axis=1))/(R1-R0),0,1)
BEZR=set(a[4].split(',')) if len(a)>4 else set()
for (o,i,p) in flat:
    if o.name in BEZR: AL[vm[(o.name,i)]]=0
D=D0+AL[:,None]*(D-D0)
print('СГЛАЖ: рельеф погашен полностью у',int((AL==0).sum()),'частично у',int(((AL>0)&(AL<1)).sum()))
for o in OBJ:
    kb=o.data.shape_keys.key_blocks
    for i,v in enumerate(kb[KEY].data): v.co=tuple(np.array(kb[0].data[i].co)+D[vm[(o.name,i)]])
bpy.ops.wm.save_as_mainfile(filepath=out); print('СГЛАЖ: шагов',n_it,'вершин',N)
