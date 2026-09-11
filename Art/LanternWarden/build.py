"""Approved C/4 lantern guardian: native geometry, replaceable eyes, four actions."""
import bpy, bmesh, math, os, json
from mathutils import Vector, Matrix, Euler
from math import sin, cos, pi
ROOT='D:/Projects/MazeBoo'
OUT=ROOT+'/Art/LanternWarden'; GAME=ROOT+'/Assets/Characters/LanternWarden'
os.makedirs(OUT,exist_ok=True);os.makedirs(GAME,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene;sc.name='Lantern Warden';sc.unit_settings.system='METRIC';sc.render.fps=30
asset=bpy.data.collections.new('WARDEN • export');sc.collection.children.link(asset)
studio=bpy.data.collections.new('STUDIO • not exported');sc.collection.children.link(studio)
parts=[]
def mat(name,col,em=0,rough=.43):
 m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*col,1);p.inputs['Roughness'].default_value=rough
 p.inputs['Emission Color'].default_value=(*col,1);p.inputs['Emission Strength'].default_value=em
 return m
shell=mat('Warden_Shell',(1,.27,.055));face=mat('Warden_Face',(.009,.016,.035),0,.36)
rim=mat('Warden_Rim',(.23,.82,1),2.0,.26);eyes=mat('Warden_Eyes',(.42,.9,1),2.2,.25);mouth=mat('Warden_Mouth',(.003,.006,.012))
def move(o,col):
 for c in list(o.users_collection):c.objects.unlink(o)
 col.objects.link(o);return o
def mesh(name,vs,fs,material,bone='Hover'):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update()
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
 o=bpy.data.objects.new(name,me);asset.objects.link(o);me.materials.append(material);o['bone']=bone
 for p in me.polygons:p.use_smooth=True
 parts.append(o);return o
def ell(name,c,r,material,bone='Hover',rotation=None,seg=32,rings=18):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=c)
 o=move(bpy.context.object,asset);o.name=name;o.scale=r
 if rotation is not None:o.rotation_euler=rotation.to_euler()
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 o.data.materials.append(material);o['bone']=bone
 for p in o.data.polygons:p.use_smooth=True
 parts.append(o);return o
UP=Vector((0,sin(math.radians(48)),cos(math.radians(48))))
NORMAL=Vector((0,-cos(math.radians(48)),sin(math.radians(48))))
C=Vector((0,0,.76))
def fp(x,y,d):return C+Vector((x,0,0))+UP*y+NORMAL*d
# One continuous rounded shell with a genuine opening; no dark disc pasted on top.
vs=[];fs=[];N=64;ROWS=38;opening=.88
for j in range(ROWS+1):
 p=opening+(pi-opening)*j/ROWS
 for i in range(N):
  a=2*pi*i/N
  vs.append(tuple(fp(.49*sin(p)*cos(a),.54*sin(p)*sin(a),.405*cos(p))))
for j in range(ROWS):
 for i in range(N):a=j*N+i;b=j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
body=mesh('HoodBody',vs,fs,shell)
# Rounded thick lip transitions into a narrow luminous inner trim.
def ring(name,profiles,material):
 v=[];f=[]
 for rx,ry,d in profiles:
  for i in range(N):a=2*pi*i/N;v.append(tuple(fp(rx*cos(a),ry*sin(a),d)))
 for j in range(len(profiles)-1):
  for i in range(N):a=j*N+i;b=j*N+(i+1)%N;f.append((a,b,b+N,a+N))
 return mesh(name,v,f,material)
ring('Rounded hood lip',[(.49*sin(opening),.54*sin(opening),.405*cos(opening)),(.368,.405,.277),(.347,.382,.283),(.332,.365,.266),(.328,.361,.242)],shell)
ring('Inner luminous trim',[(.332,.365,.266),(.324,.357,.269),(.318,.351,.257),(.319,.352,.241)],rim)
v=[tuple(fp(0,0,.177))];f=[];steps=20
for j in range(1,steps+1):
 r=j/steps
 for i in range(N):a=2*pi*i/N;v.append(tuple(fp(.319*r*cos(a),.352*r*sin(a),.177+.064*r*r)))
for i in range(N):f.append((0,1+i,1+(i+1)%N))
for j in range(steps-1):
 for i in range(N):a=1+j*N+i;b=1+j*N+(i+1)%N;f.append((a,a+N,b+N,b))
mesh('Recessed face',v,f,face)
for s,label in [(-1,'L'),(1,'R')]:
 ell('Mitten.'+label,(s*.465,-.012,.60),(.125,.143,.153),shell,'Hand.'+label)
# A thick swept-back tail forms a readable direction marker from overhead.
points=[((0,.17,.43),.20),((.02,.29,.35),.21),((.055,.44,.33),.18),((.10,.60,.37),.135),((.16,.73,.46),.09),((.21,.80,.59),.041),((.23,.81,.67),.004)]
def catmull(t):
 i=min(int(t),len(points)-2);u=t-i
 p=[(*points[max(0,min(len(points)-1,k))][0],points[max(0,min(len(points)-1,k))][1]) for k in (i-1,i,i+1,i+2)]
 return [0.5*(2*p[1][k]+(-p[0][k]+p[2][k])*u+(2*p[0][k]-5*p[1][k]+4*p[2][k]-p[3][k])*u*u+(-p[0][k]+3*p[1][k]-3*p[2][k]+p[3][k])*u*u*u) for k in range(4)]
v=[];f=[];TS=40;S=32
for j in range(TS+1):
 t=(len(points)-1)*j/TS;x,y,z,r=catmull(t)
 before=Vector(catmull(max(0,t-.01))[:3]);after=Vector(catmull(min(len(points)-1,t+.01))[:3]);tangent=(after-before).normalized()
 u=Vector((1,0,0));w=tangent.cross(u).normalized()
 for i in range(S):a=2*pi*i/S;v.append(tuple(Vector((x,y,z))+max(.002,r)*(u*cos(a)+w*sin(a))))
for j in range(TS):
 for i in range(S):a=j*S+i;b=j*S+(i+1)%S;f.append((a,b,b+S,a+S))
f.extend([tuple(reversed(range(S))),tuple(TS*S+i for i in range(S))]);tail=mesh('Swept tail',v,f,shell,'Tail')
def tube(name,pts,r,material,bone):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=r;cu.bevel_resolution=2
 s=cu.splines.new('POLY');s.points.add(len(pts)-1)
 for p,co in zip(s.points,pts):p.co=(*co,1)
 o=bpy.data.objects.new(name,cu);asset.objects.link(o);cu.materials.append(material)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');o['bone']=bone;parts.append(o);return o
eye_rotation=Matrix(((1,0,0),tuple(UP),tuple(NORMAL))).transposed()
for s,label in [(-1,'L'),(1,'R')]:
 # Every style has the same transform, one bone and bind pose, permitting mesh swaps.
 ell('EyeOval.'+label,fp(s*.115,.03,.210),(.043,.091,.015),eyes,'Eye.'+label,eye_rotation,24,14)
 pts=[fp(s*.115+.05*cos(t),.015+.055*sin(t),.22) for t in [pi*j/24 for j in range(25)]]
 tube('EyeCrescent.'+label,pts,.012,eyes,'Eye.'+label)
 vv=[tuple(fp(s*.115+x,.03+y,.210)) for x,y in [(0,.075),(.045,0),(0,-.075),(-.045,0)]]
 vv += [tuple(fp(s*.115,.03,.225)),tuple(fp(s*.115,.03,.195))]
 mesh('EyeDiamond.'+label,vv,[(i,(i+1)%4,4) for i in range(4)]+[((i+1)%4,i,5) for i in range(4)],eyes,'Eye.'+label)
tube('Subtle smile',[fp(.043*cos(t),-.105-.025*sin(t),.197) for t in [pi*j/24 for j in range(25)]],.008,mouth,'Hover')
# Rig with independent eye bones, sleeve motion and two tail segments.
bpy.ops.object.armature_add();rig=move(bpy.context.object,asset);rig.name='LanternWarden';rig.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT');eb=rig.data.edit_bones;r=eb[0];r.name='Root';r.head=(0,0,0);r.tail=(0,0,.15)
def bone(name,head,tail,parent):
 b=eb.new(name);b.head=head;b.tail=tail;b.parent=eb[parent]
bone('Hover',(0,0,.45),(0,0,.9),'Root')
for s,label in [(-1,'L'),(1,'R')]:
 bone('Hand.'+label,(s*.39,0,.60),(s*.39,0,.8),'Hover')
 c=fp(s*.115,.03,.210);bone('Eye.'+label,c,c+UP*.08,'Hover')
bone('Tail',(0,.19,.43),(0,.19,.65),'Hover');bone('TailTip',(0,.57,.36),(0,.57,.56),'Tail')
bpy.ops.object.mode_set(mode='OBJECT')
for o in parts:
 o.parent=rig;groups={name:o.vertex_groups.new(name=name) for name in ([o['bone']] if o!=tail else ['Tail','TailTip'])}
 for vert in o.data.vertices:
  if o==tail:
   blend=max(0,min(1,(vert.co.y-.38)/.33));weights={'Tail':1-blend,'TailTip':blend}
  else:weights={o['bone']:1}
  for name,w in weights.items():
   if w>0:groups[name].add([vert.index],w,'REPLACE')
 mod=o.modifiers.new('Warden rig','ARMATURE');mod.object=rig
def pose(name,loc=(0,0,0),rot=(0,0,0),scale=(1,1,1)):
 b=rig.pose.bones[name];basis=b.bone.matrix_local.to_3x3();q=basis.to_quaternion()
 b.location=basis.inverted()@Vector(loc);b.rotation_mode='QUATERNION';b.rotation_quaternion=q.inverted()@Euler(rot).to_quaternion()@q;b.scale=scale
def reset():
 for b in rig.pose.bones:pose(b.name)
def key(frame):
 for b in rig.pose.bones:
  for path in ['location','rotation_quaternion','scale']:b.keyframe_insert(data_path=path,frame=frame)
rig.animation_data_create();actions={}
for name,length in [('Warden_Idle',90),('Warden_Move',30),('Warden_Caught',15),('Warden_Death',36)]:
 action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
 for frame in range(1,length+2):
  t=(frame-1)/length;reset()
  if name=='Warden_Idle':
   pose('Hover',loc=(0,0,.018*sin(2*pi*t)),rot=(.025*sin(2*pi*t),0,0))
   pose('Tail',rot=(.07*sin(2*pi*t),0,.06*sin(2*pi*t)))
   pose('TailTip',rot=(.12*sin(2*pi*t+.4),0,0))
   blink=max(0,1-abs(t-.65)/.045)
   for label in ['L','R']:pose('Eye.'+label,scale=(1,1-.92*blink,1))
  elif name=='Warden_Move':
   pose('Hover',loc=(0,0,.026*sin(4*pi*t)),rot=(.10+.025*sin(4*pi*t),0,0))
   pose('Tail',rot=(.20*sin(2*pi*t),0,.14*sin(2*pi*t)))
   pose('TailTip',rot=(.27*sin(2*pi*t-.7),0,.12*sin(2*pi*t-.7)))
   for s,label in [(-1,'L'),(1,'R')]:pose('Hand.'+label,rot=(.15,.13*s*sin(2*pi*t),.20*s))
  elif name=='Warden_Caught':
   impact=sin(pi*min(1,t*2.5));settle=min(1,t*2)
   pose('Hover',loc=(0,.035*impact,-.025*settle),rot=(-.28*impact,.10*sin(3*pi*t)*(1-t),0),scale=(1+.12*impact,1-.20*impact,1+.08*impact))
   pose('Tail',rot=(-.35*settle,0,.15*sin(3*pi*t)*(1-t)))
   for s,label in [(-1,'L'),(1,'R')]:
    pose('Hand.'+label,rot=(0,0,s*-.50*impact));pose('Eye.'+label,scale=(1+.18*impact,1+.25*impact,1))
  else:
   smooth=t*t*(3-2*t)
   pose('Hover',loc=(0,0,-.22*smooth),rot=(-.10*smooth,.10*sin(pi*t),.8*smooth),scale=(1-.4*smooth,1-.65*smooth,1-.4*smooth))
   pose('Tail',rot=(-.7*smooth,0,0),scale=(1-.6*smooth,1-.6*smooth,1-.6*smooth))
   for s,label in [(-1,'L'),(1,'R')]:
    pose('Hand.'+label,rot=(0,0,s*.65*smooth));pose('Eye.'+label,scale=(1,1-.98*min(1,t*2),1))
  key(frame)
 actions[name]=action
rig.animation_data.action=actions['Warden_Idle'];sc.frame_start=1;sc.frame_end=91;sc.frame_set(1)
def matrices(frame):
 sc.frame_set(frame);bpy.context.view_layer.update();return [v for b in rig.pose.bones for row in b.matrix for v in row]
checks={}
for name,length in [('Warden_Idle',90),('Warden_Move',30)]:
 rig.animation_data.action=actions[name];checks[name]=max(abs(a-b) for a,b in zip(matrices(1),matrices(length+1)));assert checks[name]<1e-5
rig.animation_data.action=actions['Warden_Idle'];sc.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for o in parts+[rig]:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=GAME+'/LanternWarden.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,
 axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
for o in parts:
 if o.name.startswith(('EyeCrescent','EyeDiamond')):o.hide_render=True;o.hide_set(True)
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.2,-4,2.8));cam=move(bpy.context.object,studio);cam.name='Portrait';cam.data.type='ORTHO';cam.data.ortho_scale=1.95;aim(cam,(0,.05,.72));sc.camera=cam
bpy.ops.object.camera_add(location=(0,0,5));top=move(bpy.context.object,studio);top.name='True overhead';top.data.type='ORTHO';top.data.ortho_scale=1.75;aim(top,(0,.12,0))
for loc,power,size,col in [((-3,-4,5),380,4,(1,.88,.75)),((3,-1,3),130,3,(.7,.86,1)),((0,3,4),300,3,(.60,.73,1))]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=move(bpy.context.object,studio);o.data.energy=power;o.data.size=size;o.data.color=col;aim(o,(0,0,.7))
bpy.ops.mesh.primitive_plane_add(size=200);floor=move(bpy.context.object,studio);floor.data.materials.append(mat('Studio',(.024,.030,.067)))
sc.world=bpy.data.worlds.new('Night blue');sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.07,.09,.14,1);sc.world.node_tree.nodes['Background'].inputs[1].default_value=.3
sc.render.engine='CYCLES';sc.cycles.samples=40;sc.cycles.use_denoising=True;sc.render.threads_mode='FIXED';sc.render.threads=8
sc.render.resolution_x=1000;sc.render.resolution_y=1000;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG';sc.view_settings.view_transform='AgX'
tree=bpy.data.node_groups.new('Controlled lantern bloom','CompositorNodeTree');tree.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
rl=tree.nodes.new('CompositorNodeRLayers');gl=tree.nodes.new('CompositorNodeGlare');gl.inputs['Type'].default_value='Fog Glow';gl.inputs['Threshold'].default_value=1.1;gl.inputs['Strength'].default_value=.22
out=tree.nodes.new('NodeGroupOutput');tree.links.new(rl.outputs['Image'],gl.inputs['Image']);tree.links.new(gl.outputs['Image'],out.inputs['Image']);sc.compositing_node_group=tree
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/LanternWarden.blend')
sc.render.filepath=OUT+'/portrait.png';bpy.ops.render.render(write_still=True)
sc.camera=top;sc.render.filepath=OUT+'/overhead.png';bpy.ops.render.render(write_still=True)
stats={'triangles_default':sum(len(p.vertices)-2 for o in parts if not o.hide_render for p in o.data.polygons),'bones':len(rig.data.bones),'loop_errors':checks,'actions':list(actions),'eye_styles':['Oval','Crescent','Diamond']}
json.dump(stats,open(OUT+'/validation.json','w'),indent=2);print('WARDEN_VALIDATION '+json.dumps(stats))
