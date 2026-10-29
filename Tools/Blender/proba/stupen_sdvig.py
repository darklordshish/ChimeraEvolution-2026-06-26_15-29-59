"""СДВИГ СТУПЕНЕЙ (29.10, решение геймдизайнера: прежняя ст.2 оборотня становится ст.1, новая ст.2 — новая форма; ГеймБосс,
`Docs/models/FEEDBACK-2026-10-29-codex-stupeni-zanovo.md`). Перекладка содержимого, код игры тот же:
  1. форма-ключ тела ступени 1 (`--v`, «Волк1») := форма ступени 2 (`--iz`, «Волк») на всех объектах тела (без `@`);
  2. прежний кусок-черта ступени 1 (`--kusok_v`, «@Волк1») удаляется, кусок ступени 2 (`--kusok_iz`, «@Волк») получает его имя.
Ключ `--iz` остаётся как есть — до новой цели ступень 2 рисуется тем же телом, что ступень 1.

  blender -b --factory-startup -P stupen_sdvig.py -- вход.blend выход.blend [--iz Волк] [--v Волк1] [--kusok_iz @Волк] [--kusok_v @Волк1]
"""
import sys
import bpy

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(iz='Волк', v='Волк1', kusok_iz='@Волк', kusok_v='@Волк1')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
itog = []
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys: continue
    kbs = o.data.shape_keys.key_blocks
    if opt['iz'] not in kbs: continue
    kb = kbs.get(opt['v']) or o.shape_key_add(name=opt['v'], from_mix=False)
    kb.relative_key = kbs[0]
    kb.data.foreach_set('co', [c for d in kbs[opt['iz']].data for c in d.co])
    o.data.update(); itog.append(o.name)
staryj = bpy.data.objects.get(opt['kusok_v'])
if staryj is not None: bpy.data.objects.remove(staryj, do_unlink=True)
kusok = bpy.data.objects[opt['kusok_iz']]; kusok.name = opt['kusok_v']; kusok.data.name = opt['kusok_v']
print('СДВИГ СТУПЕНИ: ключ %s := %s на %d объектах (%s); кусок %s → %s%s' % (
    opt['v'], opt['iz'], len(itog), ', '.join(itog), opt['kusok_iz'], kusok.name, ', прежний удалён' if staryj else ''))
bpy.ops.wm.save_as_mainfile(filepath=out)
