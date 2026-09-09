"""Build the approved white ghost as editable, rigged Blender geometry."""
import bpy, math, os, json, bmesh
from mathutils import Vector, Matrix
from math import sin, cos, pi

ROOT = 'D:/Projects/miniGame01'
OUT = ROOT + '/Art/LumenGhost'
GAME = ROOT + '/Assets/Characters/LumenGhost'
os.makedirs(OUT, exist_ok=True)
os.makedirs(GAME, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.name = 'Lumen • approved white ghost'
sc.unit_settings.system = 'METRIC'
sc.render.fps = 30
sc.frame_end = 91
asset = bpy.data.collections.new('CHARACTER • export')
sc.collection.children.link(asset)
studio = bpy.data.collections.new('STUDIO • render only')
sc.collection.children.link(studio)
parts = []

def material(name, color, rough=.4, emission=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = rough
    p.inputs['Emission Color'].default_value = (*color, 1)
    p.inputs['Emission Strength'].default_value = emission
    return m

pearl = material('Lumen_Pearl', (.83, .94, 1), .32, .25)
n = pearl.node_tree.nodes
l = pearl.node_tree.links
fresnel = n.new('ShaderNodeFresnel')
fresnel.inputs['IOR'].default_value = 1.32
ramp = n.new('ShaderNodeValToRGB')
ramp.color_ramp.elements[0].color = (.50, .65, .72, 1)
ramp.color_ramp.elements[1].position = .65
ramp.color_ramp.elements[1].color = (.35, 4.0, 6.5, 1)
l.new(fresnel.outputs[0], ramp.inputs[0])
l.new(ramp.outputs[0], n.get('Principled BSDF').inputs['Emission Color'])
n.get('Principled BSDF').inputs['Emission Strength'].default_value = .8
ink = material('Lumen_Ink', (.009, .020, .028), .18)
glint = material('Lumen_Glint', (.92, 1, 1), .2, .65)
teal = material('Lumen_Iris', (.012, .095, .095), .24)
tongue = material('Lumen_Smile', (.47, .16, .095), .5)

def link_asset(o):
    for c in list(o.users_collection):
        c.objects.unlink(o)
    asset.objects.link(o)
    return o

def smooth(o):
    for p in o.data.polygons:
        p.use_smooth = True
    return o

def ellipsoid(name, loc, scale, mat, segments=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    o = link_asset(bpy.context.object)
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    parts.append(o)
    return smooth(o)

def mesh(name, vertices, faces, mat):
    me = bpy.data.meshes.new(name)
    me.from_pydata(vertices, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    asset.objects.link(o)
    me.materials.append(mat)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    parts.append(o)
    return smooth(o)

# Continuous smooth cross sections from the curved tail tip to a domed head.
# (height, horizontal center, depth center, width radius, depth radius)
profile = [(.10,-.49,.09,.006,.006),(.15,-.34,.08,.065,.048),
 (.25,-.19,.065,.13,.10),(.42,-.065,.045,.22,.16),
 (.65,0,.02,.32,.245),(.90,.025,0,.40,.315),
 (1.15,.018,0,.455,.355),(1.40,0,0,.455,.35),
 (1.60,0,0,.375,.29),(1.75,0,0,.245,.19),
 (1.83,0,0,.105,.082),(1.85,0,0,.002,.002)]

def sample(t):
    i = min(int(t), len(profile)-2)
    u = t-i
    a,b,c,d = [profile[max(0,min(len(profile)-1,j))] for j in (i-1,i,i+1,i+2)]
    return [0.5*((2*b[k])+(-a[k]+c[k])*u+(2*a[k]-5*b[k]+4*c[k]-d[k])*u*u+(-a[k]+3*b[k]-3*c[k]+d[k])*u*u*u) for k in range(5)]

verts=[]; faces=[]; rows=100; sides=64
for j in range(rows+1):
    z,x,y,rx,ry = sample((len(profile)-1)*j/rows)
    for i in range(sides):
        a=2*pi*i/sides
        verts.append((x+max(.002,rx)*cos(a),y+max(.002,ry)*sin(a),z))
for j in range(rows):
    for i in range(sides):
        a=j*sides+i; b=j*sides+(i+1)%sides
        faces.append((a,b,b+sides,a+sides))
faces += [tuple(reversed(range(sides))),tuple(rows*sides+i for i in range(sides))]
body=mesh('Pearl body • flowing tail',verts,faces,pearl)

# Palm and three fingers plus thumb on each side, fused with the body.
for sign,label in [(-1,'L'),(1,'R')]:
    arm=ellipsoid('Arm '+label,(sign*.47,-.005,1.04),(.23,.135,.145),pearl)
    arm.rotation_euler[1]=sign*-.30
    ellipsoid('Palm '+label,(sign*.64,-.025,1.115),(.125,.09,.13),pearl)
    for name,p0,p1,r in [
       ('outer',(.69,-.035,1.10),(.84,-.04,1.15),.044),
       ('middle',(.68,-.025,1.15),(.81,-.03,1.26),.046),
       ('inner',(.65,-.015,1.19),(.70,-.02,1.33),.045),
       ('thumb',(.59,-.055,1.15),(.56,-.08,1.27),.05)]:
        a=Vector((sign*p0[0],p0[1],p0[2])); b=Vector((sign*p1[0],p1[1],p1[2]))
        o=ellipsoid(name+' '+label,(a+b)/2,(r,r,(b-a).length/2+r),pearl,16,12)
        o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()

bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=body
bpy.ops.object.join()
parts=[body]
remesh=body.modifiers.new('Seamless sculpt union','REMESH')
remesh.mode='VOXEL';remesh.voxel_size=.012;remesh.use_smooth_shade=True
bpy.ops.object.modifier_apply(modifier=remesh.name)
sm=body.modifiers.new('Soft sculpt finish','SMOOTH');sm.factor=1.0;sm.iterations=4
bpy.ops.object.modifier_apply(modifier=sm.name)
dec=body.modifiers.new('Game surface','DECIMATE');dec.ratio=.26
bpy.ops.object.modifier_apply(modifier=dec.name)
smooth(body)

# Facial elements follow the curved front surface, rather than a flat pasted card.
def front(x,z):
    best=min((sample((len(profile)-1)*j/1000) for j in range(1001)),key=lambda p:abs(p[0]-z))
    zz,cx,cy,rx,ry=best
    return cy-ry*math.sqrt(max(.02,1-((x-cx)/rx)**2))

def face_ell(name,x,z,size,mat,depth=.018):
    o=ellipsoid(name,(x,front(x,z)-depth,z),size,mat)
    o.rotation_euler[0]=math.radians(-25)
    o['face_part']=True
    return o

for s,label in [(-1,'L'),(1,'R')]:
    face_ell('Eye '+label,s*.17,1.43,(.090,.047,.125),ink,.014)
    face_ell('Iris '+label,s*.17,1.395,(.066,.016,.051),teal,.059)
    face_ell('Main sparkle '+label,s*.17-.022,1.486,(.025,.016,.033),glint,.062)
    face_ell('Tiny sparkle '+label,s*.17+.027,1.402,(.010,.009,.013),glint,.075)

def stroke(name,points,radius,mat):
    cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=radius;cu.bevel_resolution=3
    sp=cu.splines.new('POLY');sp.points.add(len(points)-1)
    for p,(x,z) in zip(sp.points,points):p.co=(x,front(x,z)-.021,z,1)
    o=bpy.data.objects.new(name,cu);asset.objects.link(o);cu.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH');parts.append(o);o['face_part']=True
    return o

for s,label in [(-1,'L'),(1,'R')]:
    stroke('Eyebrow '+label,[(s*(.125+.10*t),1.66-.04*t+.01*sin(pi*t)) for t in [j/16 for j in range(17)]],.012,ink)

# Open smile as a curved filled surface; a smaller warm tongue inside.
def smile_patch(name,width,ztop,height,mat,offset):
    vs=[];fs=[];N=32
    for i in range(N+1):
        u=-1+2*i/N;x=width*u
        top=ztop-.022*(1-u*u);bottom=ztop-height*(1-u*u)
        for z in [top,bottom]:vs.append((x,front(x,z)-offset,z))
    for i in range(N):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    ob=mesh(name,vs,fs,mat);ob['face_part']=True
    return ob
smile_patch('Happy open smile',.105,1.235,.105,ink,.022)
smile_patch('Warm little tongue',.054,1.170,.024,tongue,.026)

# Tip the upper body and its actual facial geometry toward the overhead camera.
# Smooth transition preserves the continuous lower body and swept tail.
for o in parts:
    world=o.matrix_world.copy(); inv=world.inverted()
    for v in o.data.vertices:
        p=world@v.co
        t=max(0,min(1,(p.z-.88)/.32));t=t*t*(3-2*t)
        pivot=Vector((0,0,1.05))
        p=pivot+Matrix.Rotation(math.radians(-40)*t,3,'X')@(p-pivot)
        v.co=inv@p

# Six bone rig: stationary root, hover, two hands and a two segment tail.
bpy.ops.object.armature_add(location=(0,0,0))
rig=link_asset(bpy.context.object);rig.name='LumenGhost';rig.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT')
eb=rig.data.edit_bones
root=eb[0];root.name='Root';root.head=(0,0,0);root.tail=(0,0,.22)
def bone(name,head,tail,parent):
    b=eb.new(name);b.head=head;b.tail=tail;b.parent=eb[parent];return b
bone('Hover',(0,0,.75),(0,0,1.4),'Root')
bone('Hand.L',(-.36,0,1.03),(-.74,0,1.17),'Hover')
bone('Hand.R',(.36,0,1.03),(.74,0,1.17),'Hover')
bone('Tail',(0,0,.75),(-.07,.045,.38),'Hover')
bone('TailTip',(-.07,.045,.38),(-.43,.09,.13),'Tail')
bpy.ops.object.mode_set(mode='OBJECT')
for o in parts:
    o.parent=rig
    groups={name:o.vertex_groups.new(name=name) for name in ['Hover','Hand.L','Hand.R','Tail','TailTip']}
    for v in o.data.vertices:
        p=o.matrix_world@v.co
        if o.get('face_part'):
            weights={'Hover':1}
        else:
            hand=max(0,min(1,(abs(p.x)-.35)/.23))*max(0,min(1,(p.z-.70)/.22))
            tail=max(0,min(1,(.80-p.z)/.40))
            tip=max(0,min(1,(.38-p.z)/.23))
            weights={'Hover':(1-hand)*(1-tail),'Hand.L' if p.x<0 else 'Hand.R':hand,
                     'Tail':(1-hand)*tail*(1-tip),'TailTip':(1-hand)*tail*tip}
        for name,w in weights.items():
            if w>0:groups[name].add([v.index],w,'REPLACE')
    mod=o.modifiers.new('Lumen deformation','ARMATURE');mod.object=rig

for frame in range(1,92,5):
    t=2*pi*(frame-1)/90
    for name in ['Hover','Hand.L','Hand.R','Tail','TailTip']:
        b=rig.pose.bones[name];b.rotation_mode='XYZ'
        b.location=(0,0,.045*sin(t)) if name=='Hover' else (0,0,0)
        b.rotation_euler=(0,0,0)
        if name.startswith('Hand'):b.rotation_euler[1]=.13*sin(t+(.5 if name.endswith('L') else -.5))
        if name=='Tail':b.rotation_euler[1]=.10*sin(t)
        if name=='TailTip':b.rotation_euler[1]=.13*sin(t+.4)
        b.keyframe_insert(data_path='location',frame=frame)
        b.keyframe_insert(data_path='rotation_euler',frame=frame)
rig.animation_data.action.name='Lumen_Hover_Loop'
sc.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for o in parts+[rig]:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=GAME+'/LumenGhost.fbx',use_selection=True,object_types={'MESH','ARMATURE'},
 add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=False,
 bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)

def studio_object(o):
    for c in list(o.users_collection):c.objects.unlink(o)
    studio.objects.link(o)
    return o
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.3,-6,2.75))
cam=studio_object(bpy.context.object);cam.name='01 Portrait';cam.data.type='ORTHO';cam.data.ortho_scale=2.65;aim(cam,(0,0,1));sc.camera=cam
bpy.ops.object.camera_add(location=(0,-.05,6))
top=studio_object(bpy.context.object);top.name='02 True board overhead';top.data.type='ORTHO';top.data.ortho_scale=2.25;aim(top,(0,-.05,0))
for loc,power,size,color in [((-3,-4,6),330,4,(.83,.94,1)),((3,-2,3),140,3,(1,.88,.72)),((0,3,4),350,3,(.24,.75,1))]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=studio_object(bpy.context.object);o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.data.color=color;aim(o,(0,0,1))
bpy.ops.mesh.primitive_plane_add(size=200)
floor=studio_object(bpy.context.object);floor.name='Navy studio floor';floor.data.materials.append(material('Studio navy',(.012,.025,.044),.8))
sc.world=bpy.data.worlds.new('Night studio');sc.world.use_nodes=True
sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.075,.12,1)
sc.world.node_tree.nodes['Background'].inputs[1].default_value=.35
sc.render.engine='CYCLES';sc.cycles.samples=40;sc.cycles.use_denoising=True
sc.render.threads_mode='FIXED';sc.render.threads=8
sc.render.resolution_x=1000;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
sc.render.image_settings.file_format='PNG';sc.view_settings.view_transform='AgX'
# Blender 5 compositor uses an explicit node group.
tree=bpy.data.node_groups.new('Lumen subtle bloom','CompositorNodeTree')
tree.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
rl=tree.nodes.new('CompositorNodeRLayers');gl=tree.nodes.new('CompositorNodeGlare')
gl.inputs['Type'].default_value='Fog Glow';gl.inputs['Quality'].default_value='High'
gl.inputs['Threshold'].default_value=.9;gl.inputs['Strength'].default_value=.45
out=tree.nodes.new('NodeGroupOutput');tree.links.new(rl.outputs['Image'],gl.inputs['Image']);tree.links.new(gl.outputs['Image'],out.inputs['Image'])
sc.compositing_node_group=tree
sc.frame_set(1)
def pose(f):
    sc.frame_set(f);bpy.context.view_layer.update()
    return [v for b in rig.pose.bones for row in b.matrix for v in row]
loop_error=max(abs(a-b) for a,b in zip(pose(1),pose(91)))
assert loop_error<1e-5,loop_error
sc.frame_set(1)
stats={'triangles':sum(len(p.vertices)-2 for o in parts for p in o.data.polygons),
       'mesh_objects':len(parts),'bones':len(rig.data.bones),'loop_error':loop_error,
       'height_m':1.85,'recommended_unity_scale':.5,'animation':'Lumen_Hover_Loop, 3 seconds, stationary root',
       'reference':'User supplied cute-halloween-3d-ghost_23-2151848549.avif and approved luminous concept'}
with open(OUT+'/validation.json','w') as f:json.dump(stats,f,indent=2)
# The saved file opens with the character selected and a useful material viewport.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=3.5
            area.spaces.active.region_3d.view_location=Vector((0,0,1))
            area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/LumenGhost.blend')
sc.camera=cam;sc.render.filepath=OUT+'/portrait.png';bpy.ops.render.render(write_still=True)
sc.camera=top;sc.render.filepath=OUT+'/overhead.png';bpy.ops.render.render(write_still=True)
print('LUMEN_VALIDATION '+json.dumps(stats))
