import bpy, math, os, json
from mathutils import Vector
from math import sin,cos,pi
P='D:/Projects/miniGame01/Art/FriendlyGhost'
G='D:/Projects/miniGame01/Assets/Characters/FriendlyGhost'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
sc=bpy.context.scene
def mat(name,color,rough=.4,em=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=em
 return m
cream=mat('Moonmilk',(1,.89,.65),.38,.22)
white=mat('Starlight',(1,.98,.86),.25,.6)
dark=mat('Blackberry',(.025,.018,.065),.2)
pink=mat('Peach blush',(1,.26,.36),.48,.12)
gold=mat('Honey glow',(1,.59,.12),.32,.5)
parts=[]
def uv(name,loc,scale,material,seg=32,rings=20):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc);o=bpy.context.object;o.name=name;o.scale=scale;o.data.materials.append(material)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for p in o.data.polygons:p.use_smooth=True
 parts.append(o);return o
# Rounded pear-shaped spirit with a softly scalloped floating hem.
vs=[];fs=[];N=64;R=32
for j in range(R+1):
 t=pi*j/R;r=sin(t);z=.67+.52*cos(t)
 for i in range(N):
  a=2*pi*i/N;hem=max(0,-cos(t))**3
  vs.append((.39*r*(1-.14*cos(t))*cos(a),.32*r*sin(a),z+.055*cos(5*a)*hem*r))
for j in range(R):
 for i in range(N):a=j*N+i;b=j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
me=bpy.data.meshes.new('Soft pear surface');me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new('Moonmallow Body',me);sc.collection.objects.link(o);o.data.materials.append(cream);parts.append(o)
for p in me.polygons:p.use_smooth=True
for s,label in [(-1,'L'),(1,'R')]:
 arm=uv('Hug mitten '+label,(s*.40,-.05,.66),(.17,.135,.22),cream);arm.rotation_euler[1]=s*-.7
uv('Floating tail',(0,.10,.22),(.19,.19,.18),cream)
# Face plane tips 40 degrees upward, preserving expression from overhead.
center=Vector((0,-.246,.86));up=Vector((0,.643,.766));normal=Vector((0,-.766,.643))
def face(name,x,y,scale,material,depth=0):
 pos=center+Vector((x,0,0))+up*y+normal*depth
 o=uv(name,pos,scale,material);o.rotation_euler[0]=math.radians(-40);return o
for s in [-1,1]:
 face('Velvet eye',s*.133,.025,(.069,.042,.091),dark,.040)
 face('Eye glint',s*.133-.016,.057,(.020,.012,.025),white,.080)
 face('Eye tiny glint',s*.133+.022,-.002,(.009,.008,.011),white,.080)
 face('Rosy cheek',s*.222,-.066,(.060,.014,.027),pink,.025)
def tube(name,points,r,material):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=r;cu.bevel_resolution=3;s=cu.splines.new('POLY');s.points.add(len(points)-1)
 for p,co in zip(s.points,points):p.co=(*co,1)
 o=bpy.data.objects.new(name,cu);sc.collection.objects.link(o);o.data.materials.append(material);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o.select_set(False);parts.append(o)
tube('Little smile',[center+Vector((.052*cos(t),0,0))+up*(-.066-.030*sin(t))+normal*.079 for t in [pi*i/24 for i in range(25)]],.009,dark)
# A small warm star rests on the forehead like a tuft of light.
starcenter=Vector((0,-.095,1.173));v=[]
for depth in [-.022,.022]:
 for i in range(10):
  a=pi/2+2*pi*i/10;r=.085 if i%2==0 else .041;v.append(tuple(starcenter+Vector((r*cos(a),depth,r*sin(a)))))
f=[tuple(range(9,-1,-1)),tuple(range(10,20))]+[(i,(i+1)%10,(i+1)%10+10,i+10) for i in range(10)]
me=bpy.data.meshes.new('Star');me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new('Wishing star',me);sc.collection.objects.link(o);o.data.materials.append(gold);parts.append(o)
be=o.modifiers.new('Soft star edges','BEVEL');be.width=.008;be.segments=3;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=be.name)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:
 bpy.context.view_layer.objects.active=o;o.select_set(True)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False)
for o in parts:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=G+'/Moonmallow.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,apply_scale_options='FBX_SCALE_UNITS')
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1.7,-3.5,2.2));cam=bpy.context.object;cam.name='Portrait';aim(cam,(0,0,.68));cam.data.type='ORTHO';cam.data.ortho_scale=1.9;sc.camera=cam
for loc,power,size,col in [((-3,-4,5),450,4,(.78,.88,1)),((3,-1,2),220,3,(1,.76,.53)),((0,3,4),550,3,(.67,.79,1))]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.data.color=col;aim(o,(0,0,.6))
sc.world.color=(.14,.14,.14);sc.render.engine='CYCLES';sc.cycles.samples=32;sc.cycles.use_denoising=True;sc.render.threads_mode='FIXED';sc.render.threads=6
sc.view_settings.view_transform='AgX';sc.render.resolution_x=800;sc.render.resolution_y=800;sc.render.resolution_percentage=100
sc.render.image_settings.file_format='PNG';sc.render.film_transparent=True
bpy.ops.wm.save_as_mainfile(filepath=P+'/Moonmallow.blend');sc.render.filepath=P+'/portrait.png';bpy.ops.render.render(write_still=True)
cam.location=(0,-.05,4);aim(cam,(0,-.05,0));cam.data.ortho_scale=1.6;sc.render.filepath=P+'/overhead.png';bpy.ops.render.render(write_still=True)
json.dump({'mesh_objects':len(parts),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts),'export':'Moonmallow.fbx','forward':'Unity +Z','height_m':1.26},open(P+'/validation.json','w'),indent=2)
