"""Build the overhead-first revision with Blender's native mesh/rig/export APIs."""
import bpy, math, json, os
from mathutils import Vector, Matrix
from math import sin, cos, pi
OUT='D:/Projects/miniGame01/Art/GhostEnemy/DuskProwler'
GAME='D:/Projects/miniGame01/Assets/Characters/DuskProwler'
os.makedirs(OUT,exist_ok=True);os.makedirs(GAME,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.name='Dusk Prowler | Overhead studio'
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.fps=30;scene.frame_start=1;scene.frame_end=61
asset=bpy.data.collections.new('CHARACTER • export only');scene.collection.children.link(asset)
studio=bpy.data.collections.new('PREVIEW • excluded from FBX');scene.collection.children.link(studio)
def material(name,rgb):
    m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True
    n=m.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial')
    # FBX-readable palette, fully matte. Emission holds flat colours in the unlit studio.
    e=n.new('ShaderNodeBsdfPrincipled');e.inputs['Base Color'].default_value=(*rgb,1)
    e.inputs['Roughness'].default_value=1;e.inputs['Metallic'].default_value=0;e.inputs['Specular IOR Level'].default_value=0
    e.inputs['Emission Color'].default_value=(*rgb,1);e.inputs['Emission Strength'].default_value=1
    m.node_tree.links.new(e.outputs[0],out.inputs['Surface']);return m
palette=[material('01 • Deep indigo',(.023,.028,.078)),material('02 • Charcoal violet',(.012,.012,.027)),material('03 • Near-black mask',(.0025,.003,.008)),material('04 • Ivory crown',(.78,.76,.65)),material('05 • Ember orange',(1,.22,.008))]
parts=[]
def mesh(name,v,f,mi,bone='Hover'):
    d=bpy.data.meshes.new(name);d.from_pydata(v,[],f);d.update()
    o=bpy.data.objects.new(name,d);asset.objects.link(o)
    for m in palette:d.materials.append(m)
    for p in d.polygons:p.material_index=mi;p.use_smooth=True
    o['bone']=bone;parts.append(o);return o
def catmull(a,b,c,d,t):return [0.5*((2*y)+(-x+z)*t+(2*x-5*y+4*z-w)*t*t+(-x+3*y-3*z+w)*t*t*t) for x,y,z,w in zip(a,b,c,d)]
# Sections: longitudinal position, half width, centre height, half height.
# Broad leading hood / swept shoulders -> uninterrupted narrow trailing taper.
profile=[(-.465,.008,.92,.008),(-.40,.205,.92,.15),(-.29,.315,.88,.30),(-.12,.31,.77,.35),(.07,.245,.64,.31),(.25,.165,.49,.24),(.42,.088,.34,.13),(.54,.033,.245,.041),(.595,.002,.228,.003)]
sections=[]
for j in range(len(profile)-1):
    for k in range(3):sections.append(catmull(profile[max(0,j-1)],profile[j],profile[j+1],profile[min(j+2,len(profile)-1)],k/3))
sections.append(profile[-1]);N=32;v=[];f=[]
for y,rx,z,rz in sections:
    for i in range(N):
        t=2*pi*i/N
        # Two restrained longitudinal mantle folds, broad and rounded.
        fold=1+.022*cos(4*t)
        v.append((rx*cos(t)*fold,y,z+rz*sin(t)))
for j in range(len(sections)-1):
    for i in range(N):a=j*N+i;b=j*N+(i+1)%N;f.append((a,a+N,b+N,b))
f += [tuple(range(N)),tuple(reversed([(len(sections)-1)*N+i for i in range(N)]))]
body=mesh('Hood and taper',v,f,0)
for p in body.data.polygons:
    # Five-colour toon form: underside and the two crisp lateral folds stay dark.
    z=sum(body.data.vertices[i].co.z for i in p.vertices)/len(p.vertices)
    x=sum(body.data.vertices[i].co.x for i in p.vertices)/len(p.vertices)
    y=sum(body.data.vertices[i].co.y for i in p.vertices)/len(p.vertices)
    if p.normal.z<-.1 or y>.43:p.material_index=1

# Short sleeves sweep backward rather than pointing sideways like spikes.
for side,label in [(-1,'L'),(1,'R')]:
    points=[(.255,-.20,.81,.075),(.315,-.16,.79,.085),(.373,-.11,.745,.085),(.415,-.075,.70,.07),(.44,-.06,.69,.048),(.449,-.06,.684,.024),(.45,-.06,.682,.001)]
    v=[];f=[];n=16
    for x,y,z,r in points:
        for i in range(n):
            t=2*pi*i/n;v.append((side*x,y+r*cos(t),z+r*.8*sin(t)))
    for j in range(len(points)-1):
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;f.append((a,b,b+n,a+n) if side==1 else (a,a+n,b+n,b))
    f += [tuple(reversed(range(n))),tuple((len(points)-1)*n+i for i in range(n))]
    o=mesh('Swept sleeve '+label,v,f,0,'Sleeve.'+label)
    for p in o.data.polygons:
        if sum(o.data.vertices[i].co.z for i in p.vertices)/len(p.vertices)<.73:p.material_index=1

# Mask plane faces 50 degrees above the horizon (40 degrees from vertical).
R=Matrix.Rotation(math.radians(40),3,'X');center=Vector((0,-.405,1.11))
v=[];f=[];n=32;rings=9
for j in range(rings):
    a=pi*j/(rings-1)
    for i in range(n):
        t=2*pi*i/n
        v.append(tuple(center+R@Vector((.248*sin(a)*cos(t),.142*sin(a)*sin(t),.021*cos(a)))))
for j in range(rings-1):
    for i in range(n):a=j*n+i;b=j*n+(i+1)%n;f.append((a,a+n,b+n,b))
mask=mesh('Recessed face mask',v,f,2)
# Only a thin crown crescent, never a full pale circular face border.
v=[];f=[];steps=32
for j in range(steps+1):
    t=pi*j/steps
    for r in [1,1.065,1.09]:v.append(tuple(center+R@Vector((.253*r*cos(t),.148*r*sin(t),.016))))
for j in range(steps):
    for k in range(2):a=j*3+k;f.append((a,a+1,a+4,a+3))
mesh('Ivory crown crescent',v,f,3)
# Deliberately just 3 pixels high at the thumbnail size; no second hot accent.
for s,label in [(-1,'L'),(1,'R')]:
    xy=[(s*.048,-.017),(s*.197,.024),(s*.187,-.013),(s*.052,-.045)]
    v=[tuple(center+R@Vector((x,y,.032))) for x,y in xy]
    v += [tuple(Vector(p)-R@Vector((0,0,.008))) for p in v]
    mesh('Ember slit '+label,v,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],4)

# Rig all meshes in world-space geometry, then join to a single skinned mesh.
ad=bpy.data.armatures.new('DuskProwler_Generic');rig=bpy.data.objects.new('DuskProwler',ad);asset.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None):
    b=ad.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=ad.edit_bones[parent]
bone('Root',(0,0,0),(0,0,.14));bone('Hover',(0,0,.65),(0,0,.95),'Root')
bone('Tail',(0,.15,.5),(0,.40,.5),'Hover')
for s,label in [(-1,'L'),(1,'R')]:bone('Sleeve.'+label,(s*.25,-.2,.8),(s*.40,0,.7),'Hover')
bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
for o in parts:
    g=o.vertex_groups.new(name=o['bone']);g.add(list(range(len(o.data.vertices))),1,'REPLACE')
    if o==body:
        tail=o.vertex_groups.new(name='Tail')
        for vert in o.data.vertices:
            w=max(0,min(1,(vert.co.y-.10)/.4));w=w*w*(3-2*w)
            if w:tail.add([vert.index],w,'REPLACE');g.add([vert.index],1-w,'REPLACE')
    o.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join();character=body;character.name='DuskProwler_Mesh'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Consistent outward normals and real triangulated export geometry.
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
tri=character.modifiers.new('Game triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
character.parent=rig;mod=character.modifiers.new('Generic skin','ARMATURE');mod.object=rig
for frame in range(1,62):
    # Explicit identical endpoint, rather than relying on floating-point sin(2pi).
    t=2*pi*((frame-1)%60)/60
    h=rig.pose.bones['Hover'];h.rotation_mode='XYZ';h.location=(0,.04*sin(t),0);h.rotation_euler=(0,0,math.radians(3)*sin(t))
    h.keyframe_insert('location',frame=frame);h.keyframe_insert('rotation_euler',frame=frame)
    tail=rig.pose.bones['Tail'];tail.rotation_mode='XYZ';tail.rotation_euler=(math.radians(5)*sin(t-pi/2),0,math.radians(6)*sin(t-pi/2));tail.keyframe_insert('rotation_euler',frame=frame)
    for s,label in [(-1,'L'),(1,'R')]:
        p=rig.pose.bones['Sleeve.'+label];p.rotation_mode='XYZ';p.rotation_euler=(s*.035*sin(t-pi/2),0,s*.045*sin(t));p.keyframe_insert('rotation_euler',frame=frame)
action=rig.animation_data.action;action.name='Ghost_Hover_Loop'
def pose(frame):
    scene.frame_set(frame);bpy.context.view_layer.update();return {p.name:[n for row in p.matrix for n in row] for p in rig.pose.bones}
p1=pose(1);p61=pose(61);err=max(abs(a-b) for n in p1 for a,b in zip(p1[n],p61[n]));assert err==0
roots=[pose(f)['Root'] for f in range(1,62)];assert all(r==roots[0] for r in roots)
scene.frame_set(1)
triangles=len(character.data.polygons);assert 2000<=triangles<=4000,triangles
materials=set(m for m in character.data.materials);assert len(materials)==5
hot=sum(p.area for p in character.data.polygons if character.data.materials[p.material_index]==palette[4]);surface=sum(p.area for p in character.data.polygons);assert hot/surface<.05
assert all(o.location.length==0 and all(abs(s-1)<1e-7 for s in o.scale) and all(abs(r)<1e-7 for r in o.rotation_euler) for o in [rig,character])
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);character.select_set(True);bpy.context.view_layer.objects.active=rig
# A named NLA strip exports an exact, unprefixed FBX take name.
track=rig.animation_data.nla_tracks.new();track.name='Hover';strip=track.strips.new('Ghost_Hover_Loop',1,action);strip.name='Ghost_Hover_Loop';rig.animation_data.action=None
bpy.ops.export_scene.fbx(filepath=GAME+'/DuskProwler.fbx',use_selection=True,object_types={'ARMATURE','MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,path_mode='AUTO')
track.mute=True;rig.animation_data.action=action
scene.frame_end=60
stats={'triangles':triangles,'materials':len(materials),'bones':len(ad.bones),'hot_surface_percent':100*hot/surface,'loop_boundary_max_error':err,'root_stationary_all_61_samples':True,'bob_total_m':.08,'roll_degrees':3,'tail_phase_lag_degrees':90,'clip':action.name,'fps':30,'seconds':2,'width_m':max(v.co.x for v in character.data.vertices)-min(v.co.x for v in character.data.vertices),'length_m':max(v.co.y for v in character.data.vertices)-min(v.co.y for v in character.data.vertices),'crown_m':max(v.co.z for v in character.data.vertices),'lowest_rest_vertex_m':min(v.co.z for v in character.data.vertices),'export_axes':'-Z forward / Y up','transforms_applied':True}
open(OUT+'/validation.json','w').write(json.dumps(stats,indent=2))
# Flat-colour thumbnail, true overhead, exactly 90x90; studio never exported.
world=bpy.data.worlds.new('Neutral preview');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.35,.35,.35,1);scene.world=world
wn=world.node_tree.nodes;wl=world.node_tree.links;black=wn.new('ShaderNodeBackground');black.inputs['Strength'].default_value=0
mix=wn.new('ShaderNodeMixShader');ray=wn.new('ShaderNodeLightPath');wl.new(ray.outputs['Is Camera Ray'],mix.inputs[0]);wl.new(black.outputs[0],mix.inputs[1]);wl.new(wn.get('Background').outputs[0],mix.inputs[2]);wl.new(mix.outputs[0],wn.get('World Output').inputs[0])
def cam(name,loc,target,scale):
    d=bpy.data.cameras.new(name);d.type='ORTHO';d.ortho_scale=scale;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();return o
top=cam('TOP • 90px acceptance',(0,.055,8),(0,.055,0),1.22);hero=cam('Form inspection',(1.8,-3,2.6),(0,.04,.66),1.7)
scene.camera=top;scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=False;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
scene.render.image_settings.file_format='PNG';scene.render.resolution_percentage=100;scene.render.film_transparent=False
def render(name,size,camera):
    scene.camera=camera;scene.render.resolution_x=size;scene.render.resolution_y=size;scene.render.filepath=OUT+'/'+name+'.png';bpy.ops.render.render(write_still=True)
render('overhead_90px',90,top);render('overhead_large',720,top);render('form_inspection',720,hero)
# Board colour checks at exactly the same thumbnail scale.
for name,col in [('grass',(.27,.62,.015)),('water',(.035,.36,.78))]:
    world.node_tree.nodes['Background'].inputs[0].default_value=(*col,1);render('overhead_90px_'+name,90,top)
world.node_tree.nodes['Background'].inputs[0].default_value=(.35,.35,.35,1)
scene.camera=top;scene.render.resolution_x=90;scene.render.resolution_y=90;scene.frame_set(1)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        s=area.spaces.active;s.shading.type='SOLID';s.shading.light='FLAT';s.shading.color_type='MATERIAL';s.overlay.show_overlays=False;s.region_3d.view_rotation=top.rotation_euler.to_quaternion();s.region_3d.view_distance=2;s.region_3d.view_location=(0,.055,.65);s.region_3d.view_perspective='ORTHO'
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/DuskProwler.blend')
print('DUSK_PROWLER_COMPLETE',json.dumps(stats))
