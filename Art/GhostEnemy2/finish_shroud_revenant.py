import bpy,json
OUT='D:/Projects/miniGame01/Art/GhostEnemy/ShroudRevenant'
GAME='D:/Projects/miniGame01/Assets/Characters/ShroudRevenant'
bpy.ops.wm.open_mainfile(filepath=OUT+'/ShroudRevenant.blend')
sc=bpy.context.scene;rig=bpy.data.objects['ShroudRevenant'];hi=bpy.data.objects['ShroudRevenant_DetailedMesh'];sc.frame_set(1)
old=bpy.data.objects.get('ShroudRevenant_GameMesh')
if old:bpy.data.objects.remove(old,do_unlink=True)
lod=hi.copy();lod.data=hi.data.copy();lod.name='ShroudRevenant_GameMesh';hi.users_collection[0].objects.link(lod)
bpy.ops.object.select_all(action='DESELECT');lod.select_set(True);bpy.context.view_layer.objects.active=lod
rig.data.pose_position='REST'
# Tiny eye slits must survive reduction unchanged at 90 pixels.
sc.tool_settings.mesh_select_mode=(False,False,True)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='DESELECT');bpy.ops.object.mode_set(mode='OBJECT')
for p in lod.data.polygons:p.select=lod.data.materials[p.material_index].name.startswith('05')
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.separate(type='SELECTED');bpy.ops.object.mode_set(mode='OBJECT')
eyes=next(o for o in bpy.context.selected_objects if o!=lod)
eyes.select_set(False);bpy.context.view_layer.objects.active=lod
d=lod.modifiers.new('Gameplay reduction','DECIMATE');d.ratio=.195;d.use_collapse_triangulate=True
bpy.ops.object.modifier_move_up(modifier=d.name);bpy.ops.object.modifier_apply(modifier=d.name)
eyes.select_set(True);lod.select_set(True);bpy.context.view_layer.objects.active=lod;bpy.ops.object.join()
rig.data.pose_position='POSE'
tris=sum(len(p.vertices)-2 for p in lod.data.polygons);assert 2000<=tris<=4000,tris
hi.hide_render=True;hi.hide_set(True)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
action=rig.animation_data.action;rig.animation_data.action=None;rig.animation_data.nla_tracks[0].mute=False
sc.frame_end=61
bpy.ops.export_scene.fbx(filepath=GAME+'/ShroudRevenant_Game.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0)
rig.animation_data.nla_tracks[0].mute=True;rig.animation_data.action=action;sc.frame_end=60
sc.cycles.samples=64;sc.cycles.use_denoising=False
def render(name,size,cam):
 sc.camera=bpy.data.objects[cam];sc.render.resolution_x=size;sc.render.resolution_y=size;sc.render.filepath=OUT+'/'+name+'.png';bpy.ops.render.render(write_still=True)
render('game_overhead_90px',90,'TOP • actual gameplay');render('game_overhead',720,'TOP • actual gameplay')
hi.hide_render=False;hi.hide_set(False);lod.hide_render=True;lod.hide_set(True)
sc.camera=bpy.data.objects['Reference three quarter'];sc.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');hi.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
report=json.load(open(OUT+'/validation.json'));report['game_triangles']=tris;report['game_mesh_materials']=len(set(lod.data.materials));report['detailed_mesh_preserved']=True
open(OUT+'/validation.json','w').write(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/ShroudRevenant.blend')
print('GAME_MESH_COMPLETE',tris)
