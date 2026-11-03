"""БАЗА ИЗ КЛЮЧА (03.11, поставка): форма-ключ `--imya` запекается в базу (вершины и Basis), все прочие ключи снимаются: `Поджарый` и `Поджарый·ПодъёмПлеч` служебные, в поставку не идут. Правки поджарого писали то ключ, то базу: база расходилась с ключом до 15 мм. Куски `@` не трогаются.

  blender -b вход.blend --factory-startup --python-exit-code 1 -P baza_iz_klyucha.py -- выход.blend [--imya Поджарый]
"""
import sys
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]; KEY = a[a.index('--imya') + 1] if '--imya' in a else 'Поджарый'
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys: continue
    kb = o.data.shape_keys.key_blocks
    if KEY not in kb: continue
    n = len(o.data.vertices); K = np.zeros(n * 3); kb[KEY].data.foreach_get('co', K); B = np.zeros(n * 3); o.data.vertices.foreach_get('co', B)
    d = float(np.linalg.norm((K - B).reshape(-1, 3), axis=1).max()); o.data.vertices.foreach_set('co', K); kb[0].data.foreach_set('co', K)
    for k in [k for k in kb if k != kb[0]]: o.shape_key_remove(k)
    o.data.update(); print('БАЗА: %s — база и ключ расходились до %.1f мм; ключей осталось %d' % (o.name, d * 1000, len(o.data.shape_keys.key_blocks)))
bpy.ops.wm.save_as_mainfile(filepath=out)
