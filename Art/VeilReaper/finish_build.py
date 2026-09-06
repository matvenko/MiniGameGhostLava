import bpy, math, json, os
from mathutils import Vector
from math import sin, cos, pi
BASE='D:/Projects/miniGame01/Art/VeilReaper'
exec(compile(open(BASE+'/create_veil_reaper.py',encoding='utf-8-sig').read(),BASE+'/create_veil_reaper.py','exec'))
# Pale frayed hood seam: readable against both grass and blue water.
pts=[face((.246*cos(t),.30*sin(t)+.034*max(0,sin(t))**8,.063)) for t in [2*pi*j/96 for j in range(97)]]
tube('Ash silver hood seam',pts,[.009]*len(pts),2,'Head',6)
# Swept shoulder mantle, separate strips carry delayed secondary motion.
for s,label in [(-1,'L'),(1,'R')]:
 for k in range(5):
  pts=[]; widths=[]
  for j in range(17):
   t=j/16
   pts.append((s*(.19+.036*k+.12*sin(pi*t)*(.5+k/8)),.035+.70*t+.035*k,1.0-.67*t+.045*sin(pi*t+k)))
   widths.append(.042*(1-t)**.7+.001)
  vs=[]
  for j,p in enumerate(pts):
   for q in [-1,0,1]:vs.append((p[0]+q*widths[j],p[1],p[2]+(.018 if q==0 else 0)))
  fs=[(j*3+i,j*3+i+1,(j+1)*3+i+1,(j+1)*3+i) for j in range(16) for i in range(2)]
  mesh('Wind torn mantle '+label+str(k),vs,fs,0 if k%2 else 1,'Tatter.'+label)
  if k in [0,4]:tube('Weathered mantle edge '+label+str(k),[(p[0]-widths[j],p[1],p[2]) for j,p in enumerate(pts)],[.004*(1-j/17) for j in range(17)],2,'Tatter.'+label,5)
# Retain editable source parts in a hidden collection after building the game mesh.
ad=bpy.data.armatures.new('VeilReaper_Generic');rig=bpy.data.objects.new('VeilReaper',ad);asset.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(name,h,t,parent=None):
 b=ad.edit_bones.new(name);b.head=h;b.tail=t
 if parent:b.parent=ad.edit_bones[parent]
bone('Root',(0,0,0),(0,0,.15));bone('Hover',(0,0,.65),(0,0,.9),'Root');bone('Head',(0,-.12,.96),(0,-.12,1.19),'Hover');bone('Tail',(0,.17,.48),(0,.38,.28),'Hover')
for s,label in [(-1,'L'),(1,'R')]:
 bone('Sleeve.'+label,(s*.23,-.025,.9),(s*.37,-.28,1),'Hover')
 bone('Claw.'+label,(s*.378,-.319,1.005),(s*.378,-.44,1.065),'Sleeve.'+label)
 bone('Tatter.'+label,(s*.28,.1,.91),(s*.32,.55,.50),'Hover')
bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
archive=bpy.data.collections.new('SOURCE • editable sculpt parts (hidden)');sc.collection.children.link(archive)
for o in parts:
 if o.name.startswith(('Curled finger','Opposing thumb')):o['bone']='Claw.'+('L' if ' L' in o.name else 'R')
 g=o.vertex_groups.new(name=o['bone']);g.add(list(range(len(o.data.vertices))),1,'REPLACE')
 if o['bone']=='Hover':
  tail=o.vertex_groups.new(name='Tail')
  for v in o.data.vertices:
   w=max(0,min(1,(.59-v.co.z)/.34));w=w*w*(3-2*w)
   if w:tail.add([v.index],w,'REPLACE');g.add([v.index],1-w,'REPLACE')
 copy=o.copy();copy.data=o.data.copy();archive.objects.link(copy)
 o.select_set(True)
archive.hide_render=True;archive.hide_viewport=True
bpy.context.view_layer.objects.active=body;bpy.ops.object.join();body.name='VeilReaper_Skinned'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
mod=body.modifiers.new('Triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
body.parent=rig;mod=body.modifiers.new('Deform','ARMATURE');mod.object=rig
actions=[]
def reset():
 for p in rig.pose.bones:p.rotation_mode='XYZ';p.location=(0,0,0);p.rotation_euler=(0,0,0);p.scale=(1,1,1)
def key(f):
 for p in rig.pose.bones:
  for prop in ['location','rotation_euler','scale']:p.keyframe_insert(prop,frame=f)
def begin(name):
 rig.animation_data_create();rig.animation_data.action=None;reset()
def end(name):
 a=rig.animation_data.action;a.name=name;a.use_fake_user=True;actions.append(a)
for name,period,amp in [('Hover_Loop',60,1),('Glide_Loop',40,1.65)]:
 begin(name)
 for f in range(1,period+2):
  reset();t=2*pi*((f-1)%period)/period;p=rig.pose.bones
  p['Hover'].location=(.012*sin(t),.038*sin(t),0);p['Hover'].rotation_euler=(.045*amp+.018*sin(t),0,.035*sin(t))
  p['Head'].rotation_euler=(-.025*amp+.02*sin(t-.7),0,-.02*sin(t))
  p['Tail'].rotation_euler=(.13*amp*sin(t-1.2),.06*sin(t-1.2),0)
  for s,label in [(-1,'L'),(1,'R')]:
   p['Sleeve.'+label].rotation_euler=(.035*sin(t+s*.5),s*.06*amp,s*.055*sin(t))
   p['Tatter.'+label].rotation_euler=(.12*amp*sin(t-1.4+s*.3),s*.075*sin(t-.9),s*.05*sin(t))
   p['Claw.'+label].rotation_euler=(.08*sin(t-1),0,0)
  key(f)
 end(name)
# Anticipation -> both hands sweep forward -> close claws -> hold -> release.
begin('Catch')
beats=[(1,0,0,0),(10,-.10,-.22,0),(17,.20,.38,.05),(24,.31,.68,.85),(34,.28,.72,1),(43,.12,.38,.5),(55,0,0,0)]
for f,lunge,reach,curl in beats:
 reset();p=rig.pose.bones
 # Bone local Y is vertical, local Z is world -Y (forward).
 p['Hover'].location=(0,.03*sin((f-1)/54*pi),lunge)
 p['Hover'].rotation_euler=(reach*.19,0,0);p['Head'].rotation_euler=(-reach*.22,0,0)
 p['Tail'].rotation_euler=(-reach*.28,0,0)
 for s,label in [(-1,'L'),(1,'R')]:
  p['Sleeve.'+label].rotation_euler=(reach*.38,s*reach*.20,-s*reach*.78)
  p['Claw.'+label].rotation_euler=(curl*.8,0,0)
  p['Tatter.'+label].rotation_euler=(-reach*.32,s*.1*reach,0)
 key(f)
end('Catch')
for name,f in [('ANTICIPATION',10),('REACH',17),('GRIP / gameplay contact',24),('HOLD',34),('RELEASE',43)]:sc.timeline_markers.new(name,frame=f)
checks={}
for a in actions:
 rig.animation_data.action=a;start,finish=map(int,a.frame_range)
 def sample(f):
  sc.frame_set(f);bpy.context.view_layer.update();return {p.name:[v for row in p.matrix for v in row] for p in rig.pose.bones}
 first=sample(start);last=sample(finish)
 assert max(abs(x-y) for n in first for x,y in zip(first[n],last[n]))<1e-6
 assert all(sample(f)['Root']==first['Root'] for f in range(start,finish+1))
 checks[a.name]={'frames':[start,finish],'seconds':(finish-start)/30,'root_static':True,'boundary_matches':True}
rig.animation_data.action=None
for a in actions:
 track=rig.animation_data.nla_tracks.new();track.name=a.name;track.strips.new(a.name,1,a)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=GAME+'/VeilReaper.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0)
for track in rig.animation_data.nla_tracks:track.mute=True
rig.animation_data.action=actions[0];sc.frame_start=1;sc.frame_end=60;sc.frame_set(1)
world=bpy.data.worlds.new('Blue charcoal studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.06,.09,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5;sc.world=world
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(-3,-4,6),550,4),('Rim',(3,2,4),650,3),('Face',(1,-4,3),140,2)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;aim(o,(0,0,.65))
def camera(name,loc,target,scale):
 d=bpy.data.cameras.new(name);d.type='ORTHO';d.ortho_scale=scale;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;aim(o,target);return o
hero=camera('Portrait',(1.6,-4,2.6),(0,.05,.73),1.8);top=camera('Gameplay • straight overhead',(0,.12,8),(0,.12,0),1.6)
sc.render.engine='CYCLES';sc.cycles.samples=24;sc.cycles.use_denoising=True;sc.render.threads_mode='FIXED';sc.render.threads=6;sc.view_settings.view_transform='AgX';sc.render.image_settings.file_format='PNG';sc.render.resolution_percentage=100
def render(name,size,cam):
 sc.camera=cam;sc.render.resolution_x=size;sc.render.resolution_y=size;sc.render.filepath=OUT+'/'+name+'.png';bpy.ops.render.render(write_still=True)
render('portrait',900,hero);render('overhead',720,top);render('overhead_90px',90,top)
rig.animation_data.action=actions[2];sc.frame_set(24);render('catch',900,hero);render('catch_overhead',720,top)
rig.animation_data.action=actions[0];sc.frame_set(1)
sc.camera=hero
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  s=area.spaces.active;s.shading.type='MATERIAL';s.overlay.show_overlays=False;s.region_3d.view_rotation=hero.rotation_euler.to_quaternion();s.region_3d.view_location=(0,.05,.7);s.region_3d.view_distance=2.5
stats={'triangles':sum(len(p.vertices)-2 for p in body.data.polygons),'bones':len(ad.bones),'width_m':max(v.co.x for v in body.data.vertices)-min(v.co.x for v in body.data.vertices),'crown_m':max(v.co.z for v in body.data.vertices),'lowest_rest_vertex_m':min(v.co.z for v in body.data.vertices),'clips':checks}
open(OUT+'/validation.json','w').write(json.dumps(stats,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/VeilReaper.blend');print('VEIL_REAPER_COMPLETE',json.dumps(stats))
