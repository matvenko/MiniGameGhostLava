import bpy,json,math
from mathutils import Vector
P='D:/Projects/miniGame01/Art/VeilReaper';G='D:/Projects/miniGame01/Assets/Characters/VeilReaper'
bpy.ops.wm.open_mainfile(filepath=P+'/VeilReaper.blend');sc=bpy.context.scene;rig=bpy.data.objects['VeilReaper'];hi=bpy.data.objects['VeilReaper_Skinned'];sc.frame_set(1)
lod=hi.copy();lod.data=hi.data.copy();lod.name='VeilReaper_GameMesh';hi.users_collection[0].objects.link(lod)
bpy.ops.object.select_all(action='DESELECT');lod.select_set(True);bpy.context.view_layer.objects.active=lod
rig.data.pose_position='REST'
dec=lod.modifiers.new('Game reduction','DECIMATE');dec.ratio=.30;dec.use_collapse_triangulate=True
bpy.ops.object.modifier_move_up(modifier=dec.name);bpy.ops.object.modifier_apply(modifier=dec.name)
rig.data.pose_position='POSE';rig.select_set(True);bpy.context.view_layer.objects.active=rig
rig.animation_data.action=None
for t in rig.animation_data.nla_tracks:t.mute=False
bpy.ops.export_scene.fbx(filepath=G+'/VeilReaper_Game.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0)
for t in rig.animation_data.nla_tracks:t.mute=True
rig.animation_data.action=bpy.data.actions['Hover_Loop'];hi.hide_render=True;hi.hide_set(True)
top=bpy.data.objects['Gameplay • straight overhead'];top.data.ortho_scale=2.1;top.location.y=-.10
def render(name,size,cam):
 sc.camera=cam;sc.render.resolution_x=size;sc.render.resolution_y=size;sc.render.filepath=P+'/'+name+'.png';bpy.ops.render.render(write_still=True)
render('game_overhead',720,top);render('game_overhead_90px',90,top)
lod.hide_render=True;lod.hide_set(True);hi.hide_render=False;hi.hide_set(False)
render('overhead',720,top)
rig.animation_data.action=bpy.data.actions['Catch'];sc.frame_set(24);render('catch_overhead',720,top)
rig.animation_data.action=bpy.data.actions['Hover_Loop'];sc.frame_set(1);sc.camera=bpy.data.objects['Portrait']
stats=json.load(open(P+'/validation.json'));stats['game_triangles']=sum(len(p.vertices)-2 for p in lod.data.polygons);open(P+'/validation.json','w').write(json.dumps(stats,indent=2))
# Whole-animation camera previews in Blender: switch active action from the Action Editor.
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.shading.type='SOLID';area.spaces.active.shading.color_type='MATERIAL';area.spaces.active.shading.show_cavity=True
bpy.ops.wm.save_as_mainfile(filepath=P+'/VeilReaper.blend')
# Actual exported board geometry and textures, rendered in Blender.
bpy.ops.wm.open_mainfile(filepath='D:/Projects/miniGame01/Art/BlenderExport/LavaScene_Environment.blend')
sc=bpy.context.scene
with bpy.data.libraries.load(P+'/VeilReaper.blend',link=False) as (src,dst):dst.collections=[n for n in src.collections if n.startswith('VEIL REAPER')]
for col in dst.collections:sc.collection.children.link(col)
rig=bpy.data.objects['VeilReaper'];rig.location=(-5.48,3.5,.42);rig.animation_data.action=bpy.data.actions.get('Hover_Loop');sc.frame_set(1)
hi=bpy.data.objects['VeilReaper_Skinned'];hi.hide_render=True
lod=bpy.data.objects['VeilReaper_GameMesh'];lod.hide_render=False;lod.hide_set(False)
camdata=bpy.data.cameras.new('Enemy overhead context');cam=bpy.data.objects.new('Enemy overhead context',camdata);sc.collection.objects.link(cam);cam.location=(-5.48,3.5,11.42);cam.rotation_euler=(0,0,0);camdata.type='ORTHO';camdata.ortho_scale=4
sc.render.engine='CYCLES';sc.cycles.samples=24;sc.cycles.use_denoising=True;sc.render.threads_mode='FIXED';sc.render.threads=6;sc.render.resolution_percentage=100
render('environment_overhead',900,cam)
camdata.type='PERSP';camdata.lens=31.1769;camdata.sensor_width=36
render('environment_camera_11m',900,cam)
bpy.ops.wm.save_as_mainfile(filepath=P+'/VeilReaper_EnvironmentPreview.blend')
print('POLISH_COMPLETE',stats)
