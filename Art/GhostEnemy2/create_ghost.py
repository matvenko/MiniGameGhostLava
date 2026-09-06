import bpy, math, json, os
from mathutils import Vector, Quaternion
from math import sin, cos, pi

OUT = 'D:/Projects/miniGame01/Art/GhostEnemy'
GAME = 'D:/Projects/miniGame01/Assets/Characters/SpectralHunter'
os.makedirs(OUT, exist_ok=True)
os.makedirs(GAME, exist_ok=True)
# A new scene preserves any existing work in the open Blender session.
scene = bpy.data.scenes.new('Spectral Hunter • asset studio')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.render.fps = 30
scene.frame_start, scene.frame_end = 1, 60
asset = bpy.data.collections.new('SpectralHunter | EXPORT')
scene.collection.children.link(asset)
studio = bpy.data.collections.new('Studio | DO NOT EXPORT')
scene.collection.children.link(studio)

def move(obj, coll):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    coll.objects.link(obj)

def mat(name, color, emission=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=.42
    p.inputs['Emission Color'].default_value=(*color,1)
    p.inputs['Emission Strength'].default_value=emission
    return m
shell=mat('Porcelain • pale lilac',(.67,.53,.92),.15)
rim=mat('Spectral edge • ivory',(.91,.84,1),.2)
dark=mat('Face • midnight plum',(.025,.007,.06))
eye=mat('Hostile gaze • coral', (1,.065,.14),2)
tip=mat('Tail • violet',(.27,.055,.52),.2)
meshes=[]
def mesh(name,vs,fs,material,bone='Hover'):
    d=bpy.data.meshes.new(name); d.from_pydata(vs,[],fs); d.update()
    o=bpy.data.objects.new(name,d); asset.objects.link(o); d.materials.append(material)
    for p in d.polygons:p.use_smooth=True
    o['bone']=bone; meshes.append(o); return o
def uv(name,loc,scale,material,bone='Hover',rotation=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=loc)
    o=bpy.context.object; o.name=name; o.scale=scale
    if rotation:o.rotation_euler=rotation
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    move(o,asset); o.data.materials.append(material)
    for p in o.data.polygons:p.use_smooth=True
    o['bone']=bone; meshes.append(o); return o

# Broad dome narrowing into an asymmetrical swept ectoplasm tail.
rings=[(.16,.035,.36),(.23,.12,.27),(.35,.23,.15),(.49,.29,.06),(.68,.32,0),(.86,.335,0),(1.02,.29,0),(1.14,.20,0),(1.20,.07,0),(1.21,.001,0)]
vs=[]; fs=[]; N=40
for j,(z,r,cy) in enumerate(rings):
    for i in range(N):
        t=2*pi*i/N
        wave=.025*sin(t*3+.5)*(1-j/(len(rings)-1))
        vs.append((r*cos(t),cy+r*.88*sin(t),z+wave))
for j in range(len(rings)-1):
    for i in range(N):a=j*N+i;b=j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
fs.extend([tuple(reversed(range(N))),tuple((len(rings)-1)*N+i for i in range(N))])
body=mesh('Mantle • swept silhouette',vs,fs,shell)
body.data.materials.append(tip)
for p in body.data.polygons:
    if p.center.z < .27: p.material_index=1

# Face points upward: its normal is 37 degrees from vertical.
ang=math.radians(37)
from mathutils import Matrix
R=Matrix.Rotation(ang,3,'X'); center=Vector((0,-.205,1.065))
uv('Ivory face rim',center,(.273,.243,.052),rim,rotation=(ang,0,0))
uv('Deep face mask',center+R@Vector((0,0,.035)),(.238,.207,.036),dark,rotation=(ang,0,0))
for s in [-1,1]:
    # Slanted wedge eyes remain large at 40 pixel gameplay scale.
    pts=[(s*.038,.04),(s*.181,.095),(s*.169,.005),(s*.066,-.014)]
    ev=[tuple(center+R@Vector((x,y,.079))) for x,y in pts]
    ev += [tuple(Vector(v)+R@Vector((0,0,.009))) for v in ev]
    mesh('Coral eye '+str(s),ev,[(4,5,6,7),(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],eye)
uv('Mouth • small howl',center+R@Vector((0,-.11,.07)),(.044,.05,.015),dark,rotation=(ang,0,0))

def tendril(name,points,radii,material,bone):
    verts=[]; faces=[]; n=12
    for (x,y,z),r in zip(points,radii):
        for i in range(n):
            t=2*pi*i/n;verts.append((x,y+r*cos(t),z+r*sin(t)))
    for j in range(len(points)-1):
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;faces.append((a,b,b+n,a+n))
    faces += [tuple(reversed(range(n))),tuple((len(points)-1)*n+i for i in range(n))]
    return mesh(name,verts,faces,material,bone)
for s,label in [(-1,'L'),(1,'R')]:
    tendril('Reaching sleeve '+label,[(s*.25,.015,.72),(s*.36,.015,.68),(s*.44,-.025,.73),(s*.48,-.11,.81)],[.12,.105,.065,.002],shell,'Sleeve.'+label)
tendril('Trailing wisp',[(0,.24,.3),(.035,.37,.24),(.10,.46,.29),(.15,.51,.40)],[.085,.066,.04,.001],tip,'Tail')

# Armature: stationary root; hover and appendages animate locally.
ad=bpy.data.armatures.new('SpectralHunter_Rig'); rig=bpy.data.objects.new('SpectralHunter',ad);asset.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None):
    b=ad.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=ad.edit_bones[parent]
bone('Root',(0,0,0),(0,0,.15))
bone('Hover',(0,0,.55),(0,0,.95),'Root')
bone('Tail',(0,.13,.38),(0,.4,.25),'Hover')
for s,label in [(-1,'L'),(1,'R')]:bone('Sleeve.'+label,(s*.25,0,.72),(s*.46,-.07,.77),'Hover')
bpy.ops.object.mode_set(mode='OBJECT')
for o in meshes:
    o.parent=rig
    g=o.vertex_groups.new(name=o['bone']);g.add(list(range(len(o.data.vertices))),1,'REPLACE')
    if o==body:
        tg=o.vertex_groups.new(name='Tail')
        for v in o.data.vertices:
            w=max(0,min(1,(.48-v.co.z)/.29))
            if w:tg.add([v.index],w,'REPLACE');g.add([v.index],1-w,'REPLACE')
    m=o.modifiers.new('Spectral deformation','ARMATURE');m.object=rig
for frame in range(1,62):
    t=2*pi*(frame-1)/60
    h=rig.pose.bones['Hover'];h.rotation_mode='XYZ'
    # Bone local Y follows world vertical.
    h.location=(.014*sin(t),.055*sin(t),0)
    h.rotation_euler=(.025*sin(t),.045*sin(t),.025*cos(t))
    h.keyframe_insert('location',frame=frame);h.keyframe_insert('rotation_euler',frame=frame)
    for name,phase,amount in [('Tail',-.8,.15),('Sleeve.L',.5,.13),('Sleeve.R',-.5,-.13)]:
        p=rig.pose.bones[name];p.rotation_mode='XYZ';p.rotation_euler=(amount*sin(t+phase),0,.06*cos(t+phase))
        p.keyframe_insert('rotation_euler',frame=frame)
action=rig.animation_data.action;action.name='Ghost_Hover_Loop';action.use_fake_user=True
scene.frame_set(1)
rig['Usage']='Unity: Generic rig, Loop Time, no root motion. 1 unit = 1 metre. Forward -Y in Blender / +Z in Unity.'

bpy.ops.object.select_all(action='DESELECT')
for o in [rig]+meshes:o.select_set(True)
bpy.context.view_layer.objects.active=rig
# Include matching end key in export; playback 1..60 avoids a duplicate pose.
scene.frame_end=61
bpy.ops.export_scene.fbx(filepath=GAME+'/SpectralHunter.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
scene.frame_end=60

# Studio cameras and ground are excluded from the exported game asset.
ground=mat('Studio • charcoal',(.018,.023,.04))
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Studio floor';move(floor,studio);floor.data.materials.append(ground)
world=bpy.data.worlds.new('Night studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.11,.14,.22,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5;scene.world=world
def aim(o,pt):o.rotation_euler=(Vector(pt)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,color,size in [('Key',(-3,-4,6),500,(.8,.86,1),4),('Rim',(2,2,4),650,(.6,.33,1),3),('Fill',(3,-2,2),180,(1,.35,.3),3)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=color;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;aim(o,(0,0,.6))
def camera(name,loc,pt,scale):
    d=bpy.data.cameras.new(name);d.type='ORTHO';d.ortho_scale=scale;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;aim(o,pt);return o
hero=camera('01 • Portrait',(2,-4,3.2),(0,0,.65),1.85)
top=camera('02 • Gameplay TOP',(0,0,8),(0,0,0),1.65)
scene.camera=hero;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        a.spaces.active.region_3d.view_distance=2.8;a.spaces.active.region_3d.view_location=(0,0,.65)
        a.spaces.active.region_3d.view_rotation=hero.rotation_euler.to_quaternion()
        a.spaces.active.shading.type='MATERIAL'
scene.render.filepath=OUT+'/SpectralHunter_portrait.png'
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/SpectralHunter.blend')
bpy.ops.render.render(write_still=True)
scene.camera=top;scene.render.filepath=OUT+'/SpectralHunter_top.png';bpy.ops.render.render(write_still=True)
scene.camera=hero
stats={'mesh_objects':len(meshes),'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'bones':len(ad.bones),'animation':'Ghost_Hover_Loop','fps':30,'duration_seconds':2,'source':'1m grid, player capsule height .7m radius .24m; camera offset 11m in LavaScene','files':['SpectralHunter.blend','SpectralHunter.fbx']}
open(OUT+'/validation.json','w').write(json.dumps(stats,indent=2))
print('GHOST_COMPLETE',json.dumps(stats))
