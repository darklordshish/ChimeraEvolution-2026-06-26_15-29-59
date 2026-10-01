"""Проба для консилиума: та же оболочка поля → сглаживание + прореживание (QEM) → плоские грани.
Три копии рядом: как сейчас · сглажено+прорежено до ~1200 тр · до ~600 тр. Детали (ригблоки) у всех одни и те же."""
import bpy, math, os, sys
D = os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.read_factory_settings(use_empty=True)

def load(name):
    bpy.ops.wm.obj_import(filepath=os.path.join(D, name), forward_axis='NEGATIVE_Z', up_axis='Y')
    obs = list(bpy.context.selected_objects)
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    if len(obs) > 1: bpy.ops.object.join()
    return bpy.context.view_layer.objects.active

def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)

skin = load('skin.obj'); parts = load('parts.obj')
bpy.context.view_layer.objects.active = skin
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.remove_doubles(threshold=0.002)
bpy.ops.object.mode_set(mode='OBJECT')
variants = [('как сейчас', None, 0)]
for target, smooth in ((1200, 6), (600, 6)):
    variants.append(('%d тр' % target, target, smooth))
dx = 2.6
made = []
for i, (label, target, smooth) in enumerate(variants):
    s = skin.copy(); s.data = skin.data.copy(); bpy.context.collection.objects.link(s)
    p = parts.copy(); p.data = parts.data.copy(); bpy.context.collection.objects.link(p)
    if target:
        m = s.modifiers.new('sm', 'LAPLACIANSMOOTH'); m.iterations = smooth; m.lambda_factor = 0.6; m.use_volume_preserve = True
        bpy.context.view_layer.objects.active = s; bpy.ops.object.modifier_apply(modifier='sm')
        ratio = target / max(1, tris(s))
        m = s.modifiers.new('dec', 'DECIMATE'); m.ratio = ratio; m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier='dec')
    s.location.x += i * dx; p.location.x += i * dx
    made.append((label, tris(s)))
skin.hide_render = True; parts.hide_render = True
for o in bpy.data.objects:
    if o.type == 'MESH':
        for poly in o.data.polygons: poly.use_smooth = False
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.62, 0.62, 0.64); sc.display.shading.show_cavity = False
sc.render.resolution_x, sc.render.resolution_y = 2400, 700
w = bpy.data.worlds.new('w'); sc.world = w
for view, rot in (('profile', (math.radians(90), 0, math.radians(-90))), ('q', (math.radians(78), 0, math.radians(-125)))):
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.6 * len(variants) + 0.4
    cx = dx * (len(variants) - 1) / 2
    cam.rotation_euler = rot
    d = cam.matrix_world.to_3x3() @ __import__('mathutils').Vector((0, 0, 1))
    cam.location = __import__('mathutils').Vector((cx, 0, 0.75)) + d * 12 if view == 'profile' else __import__('mathutils').Vector((cx, 0, 0.75)) + cam.rotation_euler.to_matrix() @ __import__('mathutils').Vector((0, 0, 12))
    sc.camera = cam
    sc.render.filepath = os.path.join(D, 'lowpoly_%s.png' % view)
    bpy.ops.render.render(write_still=True)
print('ВАРИАНТЫ', made)
