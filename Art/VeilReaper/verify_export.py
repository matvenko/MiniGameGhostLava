import bpy,json
P='D:/Projects/miniGame01/Art/VeilReaper'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='D:/Projects/miniGame01/Assets/Characters/VeilReaper/VeilReaper_Game.fbx')
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
assert len(rig.data.bones)==10
report={'bones':len(rig.data.bones),'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),'actions':{}}
for a in bpy.data.actions:
 rig.animation_data.action=a
 start,end=map(int,a.frame_range)
 def sample(f):
  bpy.context.scene.frame_set(f);bpy.context.view_layer.update();return {p.name:[v for row in p.matrix for v in row] for p in rig.pose.bones}
 x=sample(start);y=sample(end)
 delta=max(abs(v-w) for n in x for v,w in zip(x[n],y[n]))
 assert delta<1e-4,(a.name,delta)
 assert all(max(abs(v-w) for v,w in zip(sample(f)['Root'],x['Root']))<1e-5 for f in range(start,end+1))
 report['actions'][a.name]={'frames':[start,end],'boundary_error':delta,'root_static':True}
assert len(report['actions'])==3,report
open(P+'/fbx_roundtrip.json','w').write(json.dumps(report,indent=2));print('ROUNDTRIP_OK',report)
