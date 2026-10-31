import bpy,sys
p=sys.argv[sys.argv.index('--')+1];bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=p,axis_forward='-Z',axis_up='Y',automatic_bone_orientation=False)
for o in bpy.data.objects:
 if o.type=='MESH':print('MESH',o.name,'tris',sum(len(f.vertices)-2 for f in o.data.polygons),'keys',list(o.data.shape_keys.key_blocks.keys()) if o.data.shape_keys else [])
