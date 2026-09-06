import bpy,json
P='D:/Projects/miniGame01/Art/PinkGhost'
d=json.load(open(P+'/source.json'));bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
vs=[(p['x'],p['y'],p['z']) for p in d['vertices']];tri=d['triangles'];fs=[tri[i:i+3] for i in range(0,len(tri),3)]
me=bpy.data.meshes.new('Original pink ghost');me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new('Pink ghost refined original',me);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
uv=me.uv_layers.new(name='UVMap')
for l in me.loops:
 p=d['uv'][l.vertex_index];uv.data[l.index].uv=(p['x'],p['y'])
for i in range(max(d['indices'])+1):o.vertex_groups.new(name=str(i))
for i in range(len(vs)):
 for j in range(4):
  w=d['weights'][4*i+j]
  if w>0:o.vertex_groups[d['indices'][4*i+j]].add([i],w,'REPLACE')
# Weld only identical split vertices; UVs remain per corner.
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.000001);bpy.ops.object.mode_set(mode='OBJECT')
sub=o.modifiers.new('Gentle surface refinement','SUBSURF');sub.levels=1;bpy.ops.object.modifier_apply(modifier=sub.name)
for p in o.data.polygons:p.use_smooth=True
bpy.ops.wm.save_as_mainfile(filepath=P+'/PinkGhost.blend')
me=o.data;me.calc_loop_triangles();out={'vertices':[],'normals':[],'uv':[],'triangles':[],'indices':[],'weights':[]};cache={}
for t in me.loop_triangles:
 for li in t.loops:
  l=me.loops[li];v=me.vertices[l.vertex_index];uv=me.uv_layers.active.data[li].uv;key=(l.vertex_index,round(uv.x,7),round(uv.y,7))
  if key not in cache:
   cache[key]=len(out['vertices']);out['vertices'].append(dict(zip('xyz',v.co)));out['normals'].append(dict(zip('xyz',v.normal)));out['uv'].append(dict(zip('xy',uv)))
   groups=sorted([(g.group,g.weight) for g in v.groups],key=lambda x:-x[1])[:4];total=sum(w for g,w in groups);assert total>0
   groups=[(g,w/total) for g,w in groups]+[(0,0)]*(4-len(groups))
   out['indices'].extend(g for g,w in groups);out['weights'].extend(w for g,w in groups)
  out['triangles'].append(cache[key])
json.dump(out,open(P+'/refined.json','w'));print('REFINED',len(out['vertices']),len(out['triangles'])//3)
