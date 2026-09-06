import bpy, json, math
from mathutils import Vector
OUT='D:/Projects/miniGame01/Art/GhostEnemy'
scene=bpy.context.scene
rig=bpy.data.objects['SpectralHunter']
def pose(frame):
    scene.frame_set(frame);bpy.context.view_layer.update()
    return {b.name:[v for row in b.matrix for v in row] for b in rig.pose.bones}
a=pose(1);b=pose(61);c=pose(16)
error=max(abs(x-y) for n in a for x,y in zip(a[n],b[n]))
motion=max(abs(x-y) for n in a for x,y in zip(a[n],c[n]))
assert error<1e-5, error
assert motion>.01,motion
scene.frame_set(1)
stats=json.load(open(OUT+'/validation.json'))
stats.update(loop_matrix_error=error,quarter_cycle_motion=motion)
open(OUT+'/validation.json','w').write(json.dumps(stats,indent=2))
# Small-scale readability on approximate grass and water colours from LavaScene.
floor=bpy.data.objects['Studio floor'];old=floor.data.materials[0]
m=bpy.data.materials.new('Readability test • grass and water');m.use_nodes=True
nodes=m.node_tree.nodes;links=m.node_tree.links;p=nodes.get('Principled BSDF')
tex=nodes.new('ShaderNodeTexChecker');tex.inputs['Color1'].default_value=(.25,.38,.014,1);tex.inputs['Color2'].default_value=(.065,.24,.42,1);tex.inputs['Scale'].default_value=1
coord=nodes.new('ShaderNodeTexCoord');links.new(coord.outputs['Object'],tex.inputs['Vector']);links.new(tex.outputs['Color'],p.inputs['Base Color']);p.inputs['Roughness'].default_value=.8
floor.data.materials[0]=m
scene.camera=bpy.data.objects['02 • Gameplay TOP'];scene.camera.data.ortho_scale=4
scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.filepath=OUT+'/SpectralHunter_gameplay_test.png'
bpy.ops.render.render(write_still=True)
floor.data.materials[0]=old;scene.camera.data.ortho_scale=1.65
scene.camera=bpy.data.objects['01 • Portrait'];scene.render.resolution_x=900;scene.render.resolution_y=900
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
for o in bpy.data.collections['Studio | DO NOT EXPORT'].objects:o.hide_set(True)
area=bpy.context.area;area.type='VIEW_3D'
space=area.spaces.active;space.shading.type='MATERIAL';space.overlay.show_overlays=False
space.region_3d.view_distance=2.5;space.region_3d.view_location=(0,0,.65)
space.region_3d.view_rotation=scene.camera.rotation_euler.to_quaternion()
scene.render.filepath=OUT+'/SpectralHunter_portrait.png'
def save():
    bpy.ops.wm.save_as_mainfile(filepath=OUT+'/SpectralHunter.blend');return None
bpy.app.timers.register(save,first_interval=1)
print('VERIFIED seamless loop:',error,'visible movement:',motion)
