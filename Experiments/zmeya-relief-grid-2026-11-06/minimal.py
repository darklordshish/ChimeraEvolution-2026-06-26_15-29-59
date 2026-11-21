import bpy,json
from pathlib import Path
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P/'optimized-preview.blend'))
ob=bpy.data.objects['Шкура.рельеф-проба@Змея'];original=ob.data
sc=bpy.context.scene;sc.render.resolution_x=800;sc.render.resolution_y=720
def triangles(o):
    o.data.calc_loop_triangles();return len(o.data.loop_triangles)
others=sum(triangles(o) for o in bpy.data.objects if o.type=='MESH' and o!=ob and not o.hide_render)
reports=[]
for budget in [512,768]:
    ob.data=original.copy()
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Проба бюджета','DECIMATE');mod.ratio=budget/8848;mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    n=triangles(ob);reports.append(dict(skin_triangles=n,total_visible_triangles=n+others,requested_skin_budget=budget))
    sc.render.filepath=str(P/'renders'/f'minimal-{budget}.png');bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(P/f'minimal-{budget}.blend'))
(P/'minimal.json').write_text(json.dumps(reports,indent=2),encoding='utf8')
print('BUDGET',reports)
