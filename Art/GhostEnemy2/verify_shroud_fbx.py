import bpy,json
OUT='D:/Projects/miniGame01/Art/GhostEnemy/ShroudRevenant'
bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.render.fps=30
bpy.ops.import_scene.fbx(filepath='D:/Projects/miniGame01/Assets/Characters/ShroudRevenant/ShroudRevenant_Game.fbx')
objs=list(s.objects);rig=next(o for o in objs if o.type=='ARMATURE');mesh=next(o for o in objs if o.type=='MESH');act=rig.animation_data.action
def pose(f):
 s.frame_set(f);bpy.context.view_layer.update();return {b.name:[x for row in b.matrix for x in row] for b in rig.pose.bones}
a=pose(1);b=pose(61);error=max(abs(x-y) for n in a for x,y in zip(a[n],b[n]));root=all(pose(f)['Root']==a['Root'] for f in range(1,62))
tris=sum(len(p.vertices)-2 for p in mesh.data.polygons)
assert len(objs)==2 and len(rig.data.bones)==6 and 2000<=tris<=4000
assert error<1e-5 and root and 'Ghost_Hover_Loop' in act.name and list(act.frame_range)==[1,61]
report={'objects':{o.name:o.type for o in objs},'game_triangles':tris,'materials':len(mesh.data.materials),'bones':len(rig.data.bones),'imported_action':act.name,'frame_range':list(act.frame_range),'boundary_error':error,'root_stationary':root}
open(OUT+'/fbx_roundtrip.json','w').write(json.dumps(report,indent=2));print('GAME_FBX_VERIFIED',json.dumps(report))
