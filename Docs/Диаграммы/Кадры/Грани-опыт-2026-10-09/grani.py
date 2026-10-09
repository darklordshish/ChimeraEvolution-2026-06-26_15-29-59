import bpy, math, os, sys
from mathutils import Vector

D = os.path.dirname(os.path.abspath(__file__))
targets = [None, 1500, 800]          # None — как есть

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def load():
    objs = []
    for name in ("volk-field.obj", "volk-parts.obj"):
        bpy.ops.wm.obj_import(filepath=os.path.join(D, name), forward_axis='NEGATIVE_Z', up_axis='Y')
        objs += list(bpy.context.selected_objects)
    return objs

def decimate_field(objs, target):
    field = [o for o in objs if o.get("field") or o.name.startswith(("Ноги", "хребет", "Сердце", "Руки", "Пасть", "Чутьё", "Хвост", "шея", "голова", "Шкура", "Хребет"))]
    # оболочка — всё из volk-field.obj: объединяем, свариваем швы, упрощаем
    sel = [o for o in objs if o.data.get("src") == "field"]
    return sel

def setup_render(path, view):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.72, 0.72, 0.72)
    sh.show_cavity = False
    sc.render.resolution_x = 900; sc.render.resolution_y = 700
    sc.world = bpy.data.worlds.new("w") if not sc.world else sc.world
    sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
    sc.display.shading.background_type = 'VIEWPORT'
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.6
    c = Vector((0, 0.05, 0.78))
    if view == "side":
        eye = Vector((1, 0, 0))
    else:  # 3/4 спереди: морда в −Y
        eye = Vector((math.sin(math.radians(35)), -math.cos(math.radians(35)), 0.15)).normalized()
    cam.location = c + eye * 10
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

for tgt in targets:
    for view in ("side", "34front"):
        reset()
        bpy.ops.wm.obj_import(filepath=os.path.join(D, "volk-field.obj"), forward_axis='NEGATIVE_Z', up_axis='Y')
        field = list(bpy.context.selected_objects)
        bpy.ops.wm.obj_import(filepath=os.path.join(D, "volk-parts.obj"), forward_axis='NEGATIVE_Z', up_axis='Y')
        bpy.ops.object.select_all(action='DESELECT')
        for o in field: o.select_set(True)
        bpy.context.view_layer.objects.active = field[0]
        if len(field) > 1: bpy.ops.object.join()
        f = bpy.context.view_layer.objects.active
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.remove_doubles(threshold=0.002); bpy.ops.object.mode_set(mode='OBJECT')
        n0 = len(f.data.polygons)
        if tgt:
            m = f.modifiers.new("dec", 'DECIMATE'); m.decimate_type = 'COLLAPSE'
            m.ratio = min(1.0, tgt / max(1, n0)); m.use_symmetry = True; m.symmetry_axis = 'X'
            bpy.ops.object.modifier_apply(modifier="dec")
        for p in f.data.polygons: p.use_smooth = False
        out = os.path.join(D, f"r_{tgt or 'iskh'}_{view}.png")
        setup_render(out, view)
        print("ГОТОВО", tgt, view, n0, "→", len(f.data.polygons))
