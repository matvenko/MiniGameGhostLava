"""Build the approved compact fox robot for MazeBoo in Blender 5.2."""
import bpy, math, json, os
from mathutils import Vector, Matrix, Euler
from math import sin, cos, pi

ROOT = 'D:/Projects/MazeBoo'
OUT = ROOT + '/FoxRobot'
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.name = 'Fox Robot'
sc.unit_settings.system = 'METRIC'
sc.render.fps = 30
asset = bpy.data.collections.new('FOX • export')
sc.collection.children.link(asset)
studio = bpy.data.collections.new('STUDIO • render only')
sc.collection.children.link(studio)
parts = []

def material(name, color, emission=0, rough=.38):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = rough
    p.inputs['Emission Color'].default_value = (*color, 1)
    p.inputs['Emission Strength'].default_value = emission
    return m

coral = material('Fox_Coral', (1.0, .13, .042))
cream = material('Fox_Cream', (1, .87, .68))
screen = material('Fox_Screen', (.004, .009, .014), 0, .17)
cyan = material('Fox_Cyan', (.01, .86, 1), 2.5, .2)
inner = material('Fox_InnerEar', (.003, .19, .22))
shadow = material('Fox_Seam', (.19, .043, .019))

def move(obj, collection):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def ell(name, center, radii, mat, bone='Hover', seg=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=center)
    obj = move(bpy.context.object, asset)
    obj.name = name
    obj.scale = radii
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.append(mat)
    for face in obj.data.polygons: face.use_smooth = True
    obj['bone'] = bone
    parts.append(obj)
    return obj

def mesh(name, vertices, faces, mat, bone='Hover'):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    asset.objects.link(obj)
    data.materials.append(mat)
    obj['bone'] = bone
    for face in data.polygons: face.use_smooth = True
    parts.append(obj)
    return obj

def tube(name, points, radius, mat, bone='Hover'):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 20
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new('POLY')
    spline.points.add(len(points)-1)
    for p, co in zip(spline.points, points): p.co = (*co, 1)
    obj = bpy.data.objects.new(name, curve)
    asset.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    obj.data.materials.append(mat)
    obj['bone'] = bone
    parts.append(obj)
    return obj

exec(compile(open(OUT+'/reference_geometry.py').read(),OUT+'/reference_geometry.py','exec'))

# The reference side views show the whole creature tipped forward, with its tail
# rising behind it and the face looking diagonally upward.
MODEL_ROT=Matrix.Rotation(math.radians(18),4,'X')
PIVOT=Vector((0,0,.46))
def model_point(p):return PIVOT+MODEL_ROT.to_3x3()@(Vector(p)-PIVOT)
for obj in parts:
    for v in obj.data.vertices:v.co=model_point(v.co)

exec(compile(open(OUT+'/bake_occlusion.py').read(),OUT+'/bake_occlusion.py','exec'))

# Rigid weights per part keep the toy-shell geometry clean when animated.
bpy.ops.object.armature_add(location=(0,0,0))
rig=move(bpy.context.object,asset);rig.name='FoxRobot';rig.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT')
eb=rig.data.edit_bones
root=eb[0];root.name='Root';root.head=(0,0,0);root.tail=(0,0,.16)
def bone(name, head, tail, parent):
    b=eb.new(name);b.head=model_point(head);b.tail=model_point(tail);b.parent=eb[parent]
bone('Hover',(0,0,.40),(0,0,.78),'Root')
for s,label in [(-1,'L'),(1,'R')]:
    bone('Ear.'+label,(s*.255,-.18,.94),(s*.322,-.35,1.18),'Hover')
    bone('Paw.'+label,(s*.42,-.05,.5),(s*.51,-.05,.5),'Hover')
    bone('Eye.'+label,fp(s*.120,-.014,.053),fp(s*.120,.05,.053),'Hover')
bone('Smile',fp(0,-.114,.053),fp(0,-.07,.053),'Hover')
bone('Tail.1',(.185,.28,.49),(.185,.38,.49),'Hover')
bone('Tail.2',(.327,.38,.52),(.327,.47,.52),'Tail.1')
bone('Tail.3',(.434,.47,.55),(.434,.57,.55),'Tail.2')
bpy.ops.object.mode_set(mode='OBJECT')
for obj in parts:
    obj.parent=rig
    if obj==tail or obj.name.startswith('Tail '):
        groups={name:obj.vertex_groups.new(name=name) for name in ['Tail.1','Tail.2','Tail.3']}
        for vert in obj.data.vertices:
            y=(PIVOT+MODEL_ROT.to_3x3().inverted()@(vert.co-PIVOT)).y
            second=max(0,min(1,(y-.32)/.15))
            third=max(0,min(1,(y-.46)/.13))
            weights={'Tail.1':1-second,'Tail.2':second*(1-third),'Tail.3':third}
            for name,w in weights.items():
                if w>0:groups[name].add([vert.index],w,'REPLACE')
    else:
        group=obj.vertex_groups.new(name=obj['bone'])
        group.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    obj.modifiers.new('Fox rig','ARMATURE').object=rig

def pose(name, loc=(0,0,0), rot=(0,0,0), scale=(1,1,1)):
    b=rig.pose.bones[name];basis=b.bone.matrix_local.to_3x3();q=basis.to_quaternion()
    b.location=basis.inverted()@Vector(loc)
    b.rotation_mode='QUATERNION'
    b.rotation_quaternion=q.inverted()@Euler(rot).to_quaternion()@q
    b.scale=scale
def reset():
    for b in rig.pose.bones: pose(b.name)
def key(frame):
    for b in rig.pose.bones:
        for path in ['location','rotation_quaternion','scale']:
            b.keyframe_insert(data_path=path,frame=frame)
rig.animation_data_create()
actions={}
for name,length in [('Fox_Idle',90),('Fox_Move',30),('Fox_ShowOff',48),('Fox_Caught',15),('Fox_Death',36)]:
    action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
    for frame in range(1,length+2):
        t=(frame-1)/length
        reset()
        if name=='Fox_Idle':
            pose('Hover',loc=(0,0,.012*sin(2*pi*t)))
            pose('Tail.1',rot=(.14,0,.035*sin(2*pi*t)))
            blink=max(0,1-abs(t-.68)/.045)
            for label in ['L','R']: pose('Eye.'+label,scale=(1,1-.94*blink,1))
        elif name=='Fox_Move':
            # A buoyant two-step gait with alternating paws and delayed tail follow-through.
            stride=2*pi*t
            pose('Hover',loc=(0,0,.040*(1-cos(2*stride))/2),
                 rot=(.10+.025*sin(2*stride),.055*sin(stride),.018*sin(stride)))
            pose('Tail.1',rot=(.14+.025*sin(2*stride-.4),0,.065*sin(stride)))
            pose('Tail.2',rot=(.025*sin(2*stride-.8),0,.085*sin(stride-.45)))
            pose('Tail.3',rot=(0,0,.075*sin(stride-.9)))
            for sign,label in [(-1,'L'),(1,'R')]:
                pose('Paw.'+label,loc=(0,sign*.023*sin(stride),sign*.022*sin(stride)),
                     rot=(sign*.24*sin(stride),0,sign*.10*sin(stride)))
                pose('Ear.'+label,rot=(.045*sin(2*stride-.45),sign*.035*sin(stride),0))
        elif name=='Fox_ShowOff':
            # Single quick perking motion, compact tail wag and eye squint; ends at idle pose.
            burst=sin(pi*min(1,t/.72))**2 if t<.72 else 0
            pose('Hover',loc=(0,0,.046*burst),rot=(-.06*burst,0,.035*burst))
            for s,label in [(-1,'L'),(1,'R')]:
                pose('Ear.'+label,rot=(0,s*.13*burst,0))
                pose('Eye.'+label,scale=(1,1-.20*burst,1))
            pose('Tail.1',rot=(0,0,.12*burst*sin(6*pi*t)))
            pose('Tail.2',rot=(0,0,.15*burst*sin(6*pi*t-.6)))
            pose('Tail.3',rot=(0,0,.16*burst*sin(6*pi*t-1)))
            pose('Smile',scale=(1+.2*burst,1,1))
        elif name=='Fox_Caught':
            hit=sin(pi*min(1,t*2.2))
            pose('Hover',loc=(0,0,-.025*hit),rot=(-.22*hit,0,0))
            for label in ['L','R']: pose('Eye.'+label,scale=(1,1+.28*hit,1))
        else:
            smooth=t*t*(3-2*t)
            pose('Hover',loc=(0,0,-.22*smooth),rot=(0,0,.7*smooth),scale=(1-.4*smooth,1-.4*smooth,1-.65*smooth))
            for label in ['L','R']: pose('Eye.'+label,scale=(1,1-.98*min(1,t*2),1))
        key(frame)
    actions[name]=action
rig.animation_data.action=actions['Fox_Idle']
sc.frame_start=1;sc.frame_end=91;sc.frame_set(1)

def matrices(frame):
    sc.frame_set(frame);bpy.context.view_layer.update()
    return [v for b in rig.pose.bones for row in b.matrix for v in row]
checks={}
for name,length in [('Fox_Idle',90),('Fox_Move',30),('Fox_ShowOff',48)]:
    rig.animation_data.action=actions[name]
    checks[name]=max(abs(a-b) for a,b in zip(matrices(1),matrices(length+1)))
    assert checks[name]<1e-5,(name,checks[name])
rig.animation_data.action=actions['Fox_Idle'];sc.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for obj in parts+[rig]: obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=OUT+'/FoxRobot.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)

def aim(obj, point): obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.4,-4,4.4))
portrait=move(bpy.context.object,studio);portrait.name='Portrait';portrait.data.type='ORTHO';portrait.data.ortho_scale=1.72;aim(portrait,(0,.06,.60))
bpy.ops.object.camera_add(location=(0,0,5))
top=move(bpy.context.object,studio);top.name='True overhead';top.data.type='ORTHO';top.data.ortho_scale=1.70;top.location=(0,.24,5);aim(top,(0,.24,.60))
for loc,power,size,color in [((-3,-4,5),340,4,(1,.85,.73)),((3,-1,3),130,3,(.7,.9,1)),((0,3,4),250,3,(.75,.86,1))]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    o=move(bpy.context.object,studio);o.data.energy=power;o.data.size=size;o.data.color=color;aim(o,(0,0,.6))
bpy.ops.mesh.primitive_plane_add(size=200)
floor=move(bpy.context.object,studio);floor.data.materials.append(material('Studio grass',(.12,.23,.10),0,.9))
sc.world=bpy.data.worlds.new('Studio ambient');sc.world.use_nodes=True
sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.3,.35,.38,1)
sc.world.node_tree.nodes['Background'].inputs[1].default_value=.5
sc.render.engine='CYCLES';sc.cycles.samples=32;sc.cycles.use_denoising=True
sc.render.threads_mode='FIXED';sc.render.threads=8
sc.render.resolution_x=900;sc.render.resolution_y=900;sc.render.resolution_percentage=100
sc.render.image_settings.file_format='PNG';sc.view_settings.view_transform='AgX';sc.view_settings.look='AgX - Medium High Contrast'
sc.camera=portrait
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/FoxRobot.blend')
sc.render.filepath=OUT+'/portrait_final.png';bpy.ops.render.render(write_still=True)
sc.camera=top;sc.render.filepath=OUT+'/overhead_final.png';bpy.ops.render.render(write_still=True)
portrait.location=(-3.5,-4,3.2);aim(portrait,(0,.12,.6));sc.camera=portrait;sc.render.filepath=OUT+'/side_final.png';bpy.ops.render.render(write_still=True)
stats={'parts':len(parts),'triangles':sum(len(p.vertices)-2 for o in parts for p in o.data.polygons),'bones':len(rig.data.bones),'actions':list(actions),'loop_errors':checks,'unity_scale':.69}
with open(OUT+'/validation.json','w') as f:json.dump(stats,f,indent=2)
print('FOX_VALIDATION '+json.dumps(stats))

