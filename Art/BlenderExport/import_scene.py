import bpy, json, math
from pathlib import Path
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
data = json.loads((HERE / 'scene.json').read_text(encoding='utf-8-sig'))
assert not data['errors'], data['errors']
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
C = Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
def rgba(c): return tuple(c[k] for k in ('r','g','b','a'))
def xyz(v): return (v['x'],v['z'],v['y'])
materials, props = {}, {}
missing = []

def material(src, texkey=None):
    p = {v['name']:v for v in src['properties']}
    name = src['name'] + (' '+texkey if texkey else '')
    m = bpy.data.materials.new(name); m.use_nodes=True
    m['Unity shader'] = src['shader']
    n=m.node_tree.nodes; links=m.node_tree.links
    bs=n.get('Principled BSDF')
    def color(key, default): return rgba(p[key]['color']) if key in p else default
    def value(key, default): return p[key]['value'] if key in p else default
    base=color('_BaseColor',color('_Color',color('_DeepColor',(1,1,1,1))))
    bs.inputs['Base Color'].default_value=base
    m.diffuse_color=base
    bs.inputs['Roughness'].default_value=1-value('_Smoothness',0.35)
    bs.inputs['Metallic'].default_value=value('_Metallic',0)
    key=texkey or next((k for k in ('_BaseMap','_MainTex') if p.get(k,{}).get('path')),None)
    def texture(k):
        path=ROOT/p[k]['path']
        if not path.is_file(): missing.append(str(path)); return None
        t=n.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(path),check_existing=True);t.label=k
        t.location=(-500,100);return t
    if key and p.get(key,{}).get('path'):
        t=texture(key)
        if t:
            links.new(t.outputs['Color'],bs.inputs['Base Color'])
            if 'Shadow' in name:
                mult=n.new('ShaderNodeMath');mult.operation='MULTIPLY';mult.inputs[1].default_value=base[3]
                links.new(t.outputs['Alpha'],mult.inputs[0]);links.new(mult.outputs[0],bs.inputs['Alpha'])
                bs.inputs['Base Color'].default_value=(0,0,0,1)
                links.remove(bs.inputs['Base Color'].links[0])
                m.surface_render_method='DITHERED'
    emission=color('_EmissionColor',(0,0,0,1))
    bs.inputs['Emission Color'].default_value=emission
    bs.inputs['Emission Strength'].default_value=1 if max(emission[:3])>0 else 0
    if p.get('_EmissionMap',{}).get('path'):
        t=texture('_EmissionMap')
        if t: links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=max(emission[:3])
    if 'Water' in src['shader']:
        bs.inputs['Roughness'].default_value=0.28
        noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=5
        ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=color('_DeepColor',(0.03,0.15,0.4,1));ramp.color_ramp.elements[1].color=color('_LightColor',(0.2,0.5,0.65,1))
        links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],bs.inputs['Base Color'])
        bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=0.12;bump.inputs['Distance'].default_value=0.08
        links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    return m

for src in data['materials']:
    p={v['name']:v for v in src['properties']};props[src['id']]=p
    keys=[k for k in ('_TopMap','_SideMap','_SideMap0','_SideMap1','_SideMap2','_SideMap3') if p.get(k,{}).get('path')]
    materials[src['id']]={k:material(src,k) for k in keys} if keys else {None:material(src)}

collections={};objects={}; matrices={}; visible=[]
hidden=bpy.data.collections.new('Hidden source tiles (Unity render disabled)');scene.collection.children.link(hidden)
for src in data['nodes']:
    kind=src['kind']; name=src['name']; world=C@Matrix([src['matrix'][i:i+4] for i in range(0,16,4)])@C
    mesh=None
    if kind=='MESH':
        mesh=bpy.data.meshes.new(name)
        faces=[]; slots=[]
        for si,sub in enumerate(src['submeshes']):
            tr=sub['triangles']
            for j in range(0,len(tr),3):faces.append((tr[j+2],tr[j+1],tr[j]));slots.append(si)
        mesh.from_pydata([xyz(v) for v in src['vertices']],[],faces);mesh.update()
        uv=mesh.uv_layers.new(name='UVMap')
        slotmap={}
        for mid in src['materials']:
            for key,mat in materials.get(mid,{}).items():
                slotmap[(mid,key)]=len(mesh.materials);mesh.materials.append(mat)
        for poly,si in zip(mesh.polygons,slots):
            mid=src['materials'][min(si,len(src['materials'])-1)] if src['materials'] else 0
            variants=materials.get(mid,{None:None}); p=props.get(mid,{})
            normal=(world.to_3x3().inverted().transposed()@poly.normal).normalized()
            key=None
            if '_TopMap' in variants:
                key='_TopMap' if normal.z>0.55 else ('_SideMap' if '_SideMap' in variants else '_SideMap'+str(si%4))
                if key != '_TopMap' and '_SideMap0' in variants and len(src.get('uv2') or []) == len(src['vertices']):
                    variant=src['uv2'][mesh.loops[poly.loop_start].vertex_index]['x']
                    key='_SideMap'+str(max(0,min(3,int(variant+0.5))))
            poly.material_index=slotmap.get((mid,key),0)
            for li in poly.loop_indices:
                vi=mesh.loops[li].vertex_index
                local=mesh.vertices[vi].co; pos=world@local
                if key and '_SideMap0' not in variants:
                    if key=='_TopMap':
                        scale=p.get('_TopScale',{}).get('value',1) or 1;coord=(pos.x/scale,pos.y/scale)
                    else:
                        scale=p.get('_SideScale',{}).get('value',1) or 1
                        coord=((pos.y if abs(normal.x)>abs(normal.y) else pos.x)/scale,(local.z-.5)*p.get('_SideStretch',{}).get('value',1)+1+p.get('_SideOffset',{}).get('value',0))
                elif len(src['uv'])==len(src['vertices']):
                    u=src['uv'][vi];coord=(u['x'],u['y'])
                else: coord=(pos.x,pos.y)
                uv.data[li].uv=coord
        if len(src['colors'])==len(src['vertices']):
            col=mesh.color_attributes.new(name='UnityVertexColor',type='FLOAT_COLOR',domain='POINT')
            for i,c in enumerate(src['colors']):col.data[i].color=rgba(c)
        if len(src['normals'])==len(src['vertices']):mesh.normals_split_custom_set_from_vertices([xyz(v) for v in src['normals']])
    elif kind=='LIGHT':
        typ='SUN' if name.startswith('Directional') else ('SPOT' if name.startswith('Spot') else 'POINT')
        mesh=bpy.data.lights.new(name,typ);mesh.color=rgba(src['color'])[:3];mesh.energy=src['intensity']*(1 if typ=='SUN' else 100)
        world=world@Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
    elif kind=='CAMERA':
        mesh=bpy.data.cameras.new(name);mesh.type='ORTHO' if src['ortho'] else 'PERSP';mesh.ortho_scale=src['orthoSize']*2;mesh.angle=math.radians(src['fov']);mesh.clip_end=1000
        world=world@Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
    obj=bpy.data.objects.new(name,mesh);objects[src['id']]=obj;matrices[src['id']]=world
    obj['Unity root']=src['root'];obj['Unity visible']=src['visible']
    group=src['root']
    if group not in collections:
        collections[group]=bpy.data.collections.new(group);scene.collection.children.link(collections[group])
    target=hidden if kind=='MESH' and not src['visible'] else collections[group]
    target.objects.link(obj);obj.hide_render=not src['visible'];obj.hide_set(not src['visible'])
    if kind=='MESH' and src['visible']:visible.append(obj)
    if kind=='CAMERA':scene.camera=obj
for src in data['nodes']:
    obj=objects[src['id']]
    if src['parent'] in objects:obj.parent=objects[src['parent']]
    obj.matrix_world=matrices[src['id']]
hidden.hide_render=True;hidden.hide_viewport=True
bpy.context.view_layer.update()
points=[o.matrix_world@Vector(v) for o in visible for v in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=(lo+hi)/2;size=max(hi.x-lo.x,hi.y-lo.y)
camdata=bpy.data.cameras.new('Environment overview');camdata.type='ORTHO';camdata.ortho_scale=size*1.42;camdata.clip_end=1000
cam=bpy.data.objects.new('Environment overview',camdata);scene.collection.objects.link(cam)
cam.location=center+Vector((size*.32,-size*.55,size*1.2));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
scene.world=bpy.data.worlds.new('Environment World');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.28,.32,.4,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.7
scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=size*1.5
            area.spaces.active.region_3d.view_location=center
            area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
            area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.file.pack_all()
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(HERE/'preview.png')
scene.render.film_transparent=False
scene.render.image_settings.color_mode='RGBA'
scene.render.image_settings.color_depth='8'
scene['Export notes']='Unity environment snapshot without Ghost, Enemies or FriendlyGhost. Custom shaders approximated; gameplay/UI excluded. Disabled source tiles retained in a hidden collection.'
bpy.context.preferences.filepaths.save_version=0
assert len(visible)==sum(n['kind']=='MESH' and n['visible'] for n in data['nodes'])
assert not any(o.get('Unity root') in data['excluded'] for o in bpy.data.objects)
assert not missing, missing
report={'visible_meshes':len(visible),'all_meshes':sum(o.type=='MESH' for o in objects.values()),'excluded_character_roots':data['excluded'],'materials':len(bpy.data.materials),'packed_images':len([i for i in bpy.data.images if i.packed_file]),'bounds':[list(lo),list(hi)],'source':data['source'],'missing_textures':missing}
(HERE/'verification.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'LavaScene_Environment.blend'))
scene.render.filepath=str(HERE/'preview.png');bpy.ops.render.render(write_still=True)
print('EXPORT_VERIFIED',json.dumps(report))
