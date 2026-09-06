import bpy, math, json
from mathutils import Vector, Matrix
P='D:/Projects/miniGame01/Art/FriendlySpectral'
G='D:/Projects/miniGame01/Assets/Characters/FriendlySpectral'
bpy.ops.wm.open_mainfile(filepath='D:/Projects/miniGame01/Art/GhostEnemy2/SpectralHunter.blend')
sc=bpy.context.scene;rig=bpy.data.objects['SpectralHunter'];rig.name='FriendlySpectral';sc.frame_set(1)
asset=rig.users_collection[0]
palette=[('Porcelain', 'Spirit Ivory',(.83,.94,.89),.10),('Spectral edge','Pearl Rim',(.94,1,.92),.18),('Face','Deep Teal',(.022,.065,.076),.04),('Hostile','Kind Eyes',(.58,1,.83),1.1),('Tail','Jade Wisp',(.20,.60,.51),.15)]
for prefix,name,col,em in palette:
 for m in bpy.data.materials:
  if m.name.startswith(prefix):
   m.name=name;m.diffuse_color=(*col,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*col,1);p.inputs['Emission Color'].default_value=(*col,1);p.inputs['Emission Strength'].default_value=em
for o in list(asset.objects):
 if o.type=='MESH' and (o.name.startswith('Coral eye') or o.name.startswith('Mouth')):bpy.data.objects.remove(o,do_unlink=True)
R=Matrix.Rotation(math.radians(37),3,'X');center=Vector((0,-.205,1.065))
def surface(name,pts,radius,material):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=radius;cu.bevel_resolution=2;s=cu.splines.new('POLY');s.points.add(len(pts)-1)
 for p,co in zip(s.points,pts):p.co=(*tuple(center+R@Vector(co)),1)
 ob=bpy.data.objects.new(name,cu);asset.objects.link(ob);cu.materials.append(bpy.data.materials[material]);bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH')
 ob.parent=rig;g=ob.vertex_groups.new(name='Hover');g.add(list(range(len(ob.data.vertices))),1,'REPLACE');mod=ob.modifiers.new('Spectral deformation','ARMATURE');mod.object=rig
 for p in ob.data.polygons:p.use_smooth=True
# Open, round-topped luminous eyes: same recessed mask, no aggressive wedges.
for sign,label in [(-1,'L'),(1,'R')]:
 pts=[]
 for j in range(17):
  t=math.pi*j/16;pts.append((sign*.108+.044*math.cos(t),.024+.053*math.sin(t),.080))
 surface('Happy crescent '+label,pts,.016,'Kind Eyes')
surface('Gentle smile',[(.067*math.cos(t),-.054-.038*math.sin(t),.080) for t in [math.pi*j/24 for j in range(25)]],.010,'Kind Eyes')
# Round sleeve ends while preserving original spectral silhouette and bone weights.
for o in asset.objects:
 if o.type=='MESH' and o.name.startswith('Reaching sleeve'):
  for v in o.data.vertices:
   if abs(v.co.x)>.43:v.co.z-=.035*(abs(v.co.x)-.43)/.05
# Same five-bone hover as the hunter, softer amplitude.
action=rig.animation_data.action;action.name='Friendly_Hover_Loop'
sc.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
meshes=[o for o in asset.objects if o.type=='MESH']
for o in [rig]+meshes:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=rig;sc.frame_end=61
bpy.ops.export_scene.fbx(filepath=G+'/FriendlySpectral.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
sc.frame_end=60
def pose(f):
 sc.frame_set(f);bpy.context.view_layer.update();return {b.name:[v for row in b.matrix for v in row] for b in rig.pose.bones}
a=pose(1);b=pose(61);error=max(abs(x-y) for n in a for x,y in zip(a[n],b[n]));assert error<1e-5;sc.frame_set(1)
sc.render.engine='CYCLES';sc.cycles.samples=32;sc.render.threads_mode='FIXED';sc.render.threads=6
sc.render.resolution_x=800;sc.render.resolution_y=800;sc.render.resolution_percentage=100
sc.camera=bpy.data.objects['01 • Portrait'];sc.render.filepath=P+'/portrait.png'
bpy.ops.wm.save_as_mainfile(filepath=P+'/FriendlySpectral.blend');bpy.ops.render.render(write_still=True)
sc.camera=bpy.data.objects['02 • Gameplay TOP'];sc.camera.data.ortho_scale=1.65;sc.render.filepath=P+'/overhead.png';bpy.ops.render.render(write_still=True)
json.dump({'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'bones':len(rig.data.bones),'loop_error':error,'source':'Art/GhostEnemy2/SpectralHunter.blend'},open(P+'/validation.json','w'),indent=2)
