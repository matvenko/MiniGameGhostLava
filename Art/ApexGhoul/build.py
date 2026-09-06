import bpy, math, json
from mathutils import Vector,Matrix
from math import sin,cos,pi
P='D:/Projects/miniGame01/Art/ApexGhoul';G='D:/Projects/miniGame01/Assets/Characters/ApexGhoul'
bpy.ops.wm.open_mainfile(filepath='D:/Projects/miniGame01/Art/GhostEnemy2/SpectralHunter.blend')
sc=bpy.context.scene;rig=bpy.data.objects['SpectralHunter'];rig.name='ApexGhoul';asset=rig.users_collection[0];sc.frame_set(1)
palette=[('Porcelain','Obsidian Armor',(.15,.09,.26),.16),('Spectral edge','Pale Steel',(.56,.51,.72),.18),('Face','Void Mask',(.018,.012,.035),.08),('Hostile','Amber Gaze',(1,.39,.055),2),('Tail','Violet Mantle',(.29,.07,.40),.2)]
for prefix,name,col,em in palette:
 for m in bpy.data.materials:
  if m.name.startswith(prefix):
   m.name=name;m.diffuse_color=(*col,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*col,1);p.inputs['Emission Color'].default_value=(*col,1);p.inputs['Emission Strength'].default_value=em;p.inputs['Roughness'].default_value=.36
for o in list(asset.objects):
 if o.type=='MESH' and o.name.startswith(('Reaching sleeve','Mouth')):bpy.data.objects.remove(o,do_unlink=True)
def mesh(name,vs,fs,mat,bone='Hover'):
 d=bpy.data.meshes.new(name);d.from_pydata(vs,[],fs);d.update();o=bpy.data.objects.new(name,d);asset.objects.link(o);d.materials.append(bpy.data.materials[mat]);o.parent=rig
 g=o.vertex_groups.new(name=bone);g.add(list(range(len(d.vertices))),1,'REPLACE');mod=o.modifiers.new('Deform','ARMATURE');mod.object=rig
 for p in d.polygons:p.use_smooth=True
 return o
def horn(name,pts,rs,mat,bone='Hover',N=10):
 vs=[];fs=[]
 for j,p in enumerate(pts):
  tangent=Vector(pts[min(j+1,len(pts)-1)])-Vector(pts[max(0,j-1)]);tangent.normalize();u=tangent.cross(Vector((0,0,1)))
  if u.length<.01:u=tangent.cross(Vector((0,1,0)))
  u.normalize();v=tangent.cross(u)
  for i in range(N):vs.append(tuple(Vector(p)+rs[j]*(cos(2*pi*i/N)*u+sin(2*pi*i/N)*v)))
 for j in range(len(pts)-1):
  for i in range(N):a=j*N+i;b=j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
 fs.extend([tuple(reversed(range(N))),tuple((len(pts)-1)*N+i for i in range(N))]);return mesh(name,vs,fs,mat,bone)
# Swept crown: threatening silhouette without a wide collision footprint.
for s,L in [(-1,'L'),(1,'R')]:
 horn('Crown blade '+L,[(s*.18,.02,1.12),(s*.25,.10,1.30),(s*.29,.28,1.43),(s*.25,.45,1.46)],[.10,.085,.045,.001],'Pale Steel')
 horn('Crown inner '+L,[(s*.08,.10,1.17),(s*.12,.23,1.34),(s*.11,.40,1.40)],[.065,.043,.001],'Obsidian Armor')
 horn('Armored forearm '+L,[(s*.25,.02,.82),(s*.37,-.02,.70),(s*.40,-.19,.71),(s*.38,-.29,.78)],[.13,.11,.075,.055],'Obsidian Armor','Sleeve.'+L)
 horn('Shoulder swept fin '+L,[(s*.28,.045,.87),(s*.40,.20,1.01),(s*.43,.40,1.02)],[.12,.065,.001],'Pale Steel','Sleeve.'+L)
 for k in range(3):
  x=s*(.33+.038*k)
  horn('Hook claw '+L+str(k),[(x,-.25,.77),(x+s*.022,-.36,.78),(x+s*.014,-.43,.88),(x,-.40,.94)],[.027,.023,.014,.001],'Pale Steel','Sleeve.'+L,8)
 horn('Split speed wisp '+L,[(s*.12,.14,.38),(s*.18,.36,.20),(s*.21,.62,.25),(s*.19,.82,.42)],[.11,.085,.045,.001],'Violet Mantle','Tail')
 horn('Wisp hot seam '+L,[(s*.18,.37,.21),(s*.21,.59,.25),(s*.19,.78,.39)],[.014,.013,.001],'Amber Gaze','Tail',6)
R=Matrix.Rotation(math.radians(37),3,'X');C=Vector((0,-.205,1.065))
# Angular jaw with four readable fangs beneath the familiar spectral gaze.
pts=[(-.15,-.07),(-.10,-.145),(0,-.18),(.10,-.145),(.15,-.07)]
horn('Jaw armor',[tuple(C+R@Vector((x,y,.064))) for x,y in pts],[.024]*5,'Pale Steel',N=8)
for x in [-.09,-.035,.035,.09]:
 vs=[tuple(C+R@Vector(p)) for p in [(x-.017,-.075,.083),(x+.017,-.075,.083),(x,-.12 if abs(x)>.05 else -.105,.085),(x,-.077,.102)]]
 mesh('Fang',vs,[(0,1,2),(0,3,1),(1,3,2),(2,3,0)],'Pale Steel')
horn('Crown central ember',[(0,.00,1.20),(0,.14,1.33),(0,.32,1.39)],[.032,.023,.001],'Amber Gaze',N=8)
# New tighter 1-second hunt loop, root stationary; freeze logic can stop Animator.
rig.animation_data.action=None
for f in range(1,32):
 t=2*pi*(f-1)/30
 for p in rig.pose.bones:p.rotation_mode='XYZ';p.location=(0,0,0);p.rotation_euler=(0,0,0)
 h=rig.pose.bones['Hover'];h.location=(.009*sin(t),.027*sin(t),0);h.rotation_euler=(.13+.025*sin(t),.018*sin(t),.018*cos(t))
 rig.pose.bones['Tail'].rotation_euler=(.20*sin(t-.8),.06*cos(t),0)
 for s,L in [(-1,'L'),(1,'R')]:rig.pose.bones['Sleeve.'+L].rotation_euler=(.12*sin(t+s*.5),0,s*.075*cos(t))
 for p in rig.pose.bones:
  p.keyframe_insert('location',frame=f);p.keyframe_insert('rotation_euler',frame=f)
rig.animation_data.action.name='Apex_Hunt_Loop';sc.frame_set(1);sc.frame_end=31
meshes=[o for o in asset.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
 o.hide_set(False);o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False)
for o in [rig]+meshes:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=G+'/ApexGhoul.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
def pose(f):
 sc.frame_set(f);bpy.context.view_layer.update();return {b.name:[v for row in b.matrix for v in row] for b in rig.pose.bones}
a=pose(1);b=pose(31);err=max(abs(x-y) for n in a for x,y in zip(a[n],b[n]));assert err<1e-5;sc.frame_set(1);sc.frame_end=30
sc.render.engine='CYCLES';sc.cycles.samples=40;sc.render.threads_mode='FIXED';sc.render.threads=6;sc.render.resolution_x=900;sc.render.resolution_y=900;sc.render.resolution_percentage=100
sc.camera=bpy.data.objects['01 • Portrait'];sc.camera.data.ortho_scale=2.05
bpy.ops.wm.save_as_mainfile(filepath=P+'/ApexGhoul.blend');sc.render.filepath=P+'/portrait.png';bpy.ops.render.render(write_still=True)
sc.camera=bpy.data.objects['02 • Gameplay TOP'];sc.camera.data.ortho_scale=1.95;sc.render.filepath=P+'/overhead.png';bpy.ops.render.render(write_still=True)
json.dump({'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'bones':len(rig.data.bones),'loop_error':err,'duration':1,'root_stationary':a['Root']==b['Root']},open(P+'/validation.json','w'),indent=2)
