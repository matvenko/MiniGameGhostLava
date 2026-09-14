"""Bake short-range ambient occlusion into vertex colors for the mobile shader."""
from mathutils.bvhtree import BVHTree
vertices=[];polygons=[]
for obj in parts:
    start=len(vertices)
    vertices.extend(v.co.copy() for v in obj.data.vertices)
    polygons.extend(tuple(start+i for i in p.vertices) for p in obj.data.polygons)
bvh=BVHTree.FromPolygons(vertices,polygons,all_triangles=False,epsilon=.00001)
samples=24
directions=[]
for k in range(samples):
    r=math.sqrt((k+.5)/samples);a=k*2.39996323
    directions.append(Vector((r*cos(a),r*sin(a),math.sqrt(1-r*r))))
for obj in parts:
    attr=obj.data.color_attributes.new(name='FoxAO',type='FLOAT_COLOR',domain='POINT')
    for v in obj.data.vertices:
        normal=v.normal.normalized()
        basis=normal.to_track_quat('Z','Y')
        origin=v.co+normal*.0012
        occ=0
        for d in directions:
            hit,_,_,distance=bvh.ray_cast(origin,basis@d,.20)
            if hit is not None:occ+=(1-distance/.20)**.55
        ao=max(.38,1-.83*occ/samples)
        attr.data[v.index].color=(ao,ao,ao,1)
    obj.data.color_attributes.active_color=attr
print('FOX_AO baked into',len(parts),'meshes')
