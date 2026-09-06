import bpy
p='D:/Projects/miniGame01/Art/GhostEnemy/DuskProwler/DuskProwler.blend'
bpy.ops.wm.open_mainfile(filepath=p)
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':
            s=a.spaces.active;s.shading.type='SOLID';s.shading.light='FLAT';s.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=p)
