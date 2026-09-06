import bpy,json
OUT='D:/Projects/miniGame01/Art/GhostEnemy/DuskProwler'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps=30
bpy.ops.import_scene.fbx(filepath='D:/Projects/miniGame01/Assets/Characters/DuskProwler/DuskProwler.fbx')
objs=list(bpy.context.scene.objects)
rig=next(o for o in objs if o.type=='ARMATURE');mesh=next(o for o in objs if o.type=='MESH')
action=rig.animation_data.action
start,end=action.frame_range
def pose(frame):
    bpy.context.scene.frame_set(int(frame));bpy.context.view_layer.update()
    return {b.name:[n for row in b.matrix for n in row] for b in rig.pose.bones}
a=pose(start);b=pose(end)
err=max(abs(x-y) for n in a for x,y in zip(a[n],b[n]))
root=[pose(f)['Root'] for f in range(int(start),int(end)+1)]
report={'objects':{o.name:o.type for o in objs},'bones':len(rig.data.bones),'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),'materials':{m.name:list(m.diffuse_color) for m in mesh.data.materials},'imported_action':action.name,'frame_range':[start,end],'seconds':(end-start)/30,'boundary_pose_error':err,'root_stationary':all(r==root[0] for r in root)}
assert len(objs)==2 and len(rig.data.bones)==5
assert 'Ghost_Hover_Loop' in action.name and abs((end-start)/30-2)<1e-5
assert err<1e-5 and report['root_stationary']
open(OUT+'/fbx_roundtrip.json','w').write(json.dumps(report,indent=2))
print('FBX_VERIFIED',json.dumps(report))
