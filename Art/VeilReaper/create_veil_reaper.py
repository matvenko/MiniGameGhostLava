"""Reference-led ragged cloth ghost, native Blender geometry; centimetre details."""
import bpy, math, os, json
from math import sin,cos,pi
from mathutils import Vector,Matrix
OUT='D:/Projects/miniGame01/Art/VeilReaper'
GAME='D:/Projects/miniGame01/Assets/Characters/VeilReaper'
os.makedirs(OUT,exist_ok=True);os.makedirs(GAME,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene;sc.name='Veil Reaper';sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1;sc.render.fps=30;sc.frame_start=1;sc.frame_end=61
asset=bpy.data.collections.new('VEIL REAPER • export');sc.collection.children.link(asset)
studio=bpy.data.collections.new('STUDIO • not exported');sc.collection.children.link(studio)
def mat(name,col,em=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*col,1);p.inputs['Roughness'].default_value=.92;p.inputs['Specular IOR Level'].default_value=.12;p.inputs['Metallic'].default_value=0;p.inputs['Emission Color'].default_value=(*col,1);p.inputs['Emission Strength'].default_value=em;return m
mats=[mat('01 • Ash ivory shroud',(.16,.135,.23)),mat('02 • Shadowed cloth',(.035,.028,.065)),mat('03 • Aged bone',(.61,.66,.68)),mat('04 • Hollow darkness',(.004,.005,.009)),mat('05 • Cold soul eyes',(.35,.86,1.0),1.3)]
parts=[]
def mesh(name,vs,fs,mi=0,bone='Hover'):
 d=bpy.data.meshes.new(name);d.from_pydata(vs,[],fs);d.update();o=bpy.data.objects.new(name,d);asset.objects.link(o)
 for m in mats:d.materials.append(m)
 for p in d.polygons:p.material_index=mi;p.use_smooth=True
 o['bone']=bone;parts.append(o);return o
def ell(name,center,scale,mi=2,bone='Hover',R=None,seg=20,rings=12):
 R=R or Matrix.Identity(3);center=Vector(center);v=[];f=[]
 for j in range(rings+1):
  a=pi*j/rings
  for i in range(seg):
   t=2*pi*i/seg;v.append(tuple(center+R@Vector((scale[0]*sin(a)*cos(t),scale[1]*sin(a)*sin(t),scale[2]*cos(a)))))
 for j in range(rings):
  for i in range(seg):a=j*seg+i;b=j*seg+(i+1)%seg;f.append((a,a+seg,b+seg,b))
 return mesh(name,v,f,mi,bone)
def tube(name,pts,rads,mi=0,bone='Hover',sides=8):
 pts=[Vector(p) for p in pts];v=[];f=[]
 for j,p in enumerate(pts):
  tangent=(pts[min(len(pts)-1,j+1)]-pts[max(0,j-1)]).normalized();axis=Vector((0,0,1))
  if abs(tangent.dot(axis))>.94:axis=Vector((0,1,0))
  u=tangent.cross(axis).normalized();w=tangent.cross(u).normalized()
  for i in range(sides):t=2*pi*i/sides;v.append(tuple(p+rads[j]*(u*cos(t)+w*sin(t))))
 for j in range(len(pts)-1):
  for i in range(sides):a=j*sides+i;b=j*sides+(i+1)%sides;f.append((a,b,b+sides,a+sides))
 f += [tuple(reversed(range(sides))),tuple((len(pts)-1)*sides+i for i in range(sides))]
 return mesh(name,v,f,mi,bone)

# Organic fluted robe: broad shoulders taper to a curled, floating lower hem.
def robe(t,a,extra=0):
 z=1.005-.79*t
 cx=.045*sin(t*pi*1.2)*t;cy=.035+.37*t*t
 rx=.275*(1-t)**.65+.018;ry=.19*(1-t)**.55+.015
 fold=(.012+.013*sin(pi*t))*(cos(14*a+2.7*t)+.32*cos(23*a-2*t))
 r=fold+extra
 return Vector((cx+(rx+r)*cos(a),cy+(ry+r)*sin(a),z+.027*sin(a*7+1)*t**5))
N=64;rows=30;v=[];f=[]
for j in range(rows+1):
 for i in range(N):v.append(tuple(robe(j/rows,2*pi*i/N)))
for j in range(rows):
 for i in range(N):a=j*N+i;b=j*N+(i+1)%N;f.append((a,b,b+N,a+N))
body=mesh('Continuous fluted under-shroud',v,f,1)

# Overlapping cloth panels, with actual ridges and frayed saw-cut ends.
for panel in range(14):
 v=[];f=[];a0=2*pi*panel/14;length=.78+.2*(.5+.5*sin(panel*2.1));steps=20;cols=5
 for j in range(steps+1):
  t=j/steps*length
  for k in range(cols):
   u=k/(cols-1)-.5
   tt=t+(.028*(.5+.5*sin(panel*1.9+k*2.5)) if j==steps else 0)
   a=a0+.75*t+u*.52*(1-.55*t)
   p=robe(min(tt,1.04),a,.014+.009*cos(u*pi*2))
   if j==steps:p.z-=.035*(1+sin(k*3+panel))
   v.append(tuple(p))
 for j in range(steps):
  for k in range(cols-1):a=j*cols+k;f.append((a,a+1,a+cols+1,a+cols))
 mesh('Layered diagonal cloth %02d'%panel,v,f,0 if panel%4 else 1)

# Head faces the overhead camera: plane elevated 50 degrees above horizontal.
R=Matrix.Rotation(math.radians(40),3,'X');C=Vector((0,-.205,1.045))
def face(p):return C+R@Vector(p)
# Hollow hood with a deep back and a thick, uneven folded lip.
v=[];f=[];segments=64
profiles=[(1,.025),(.99,.065),(1.075,.045),(1.12,-.005),(1.13,-.075),(1.04,-.15),(.82,-.225),(.48,-.265),(.03,-.275)]
for j,(r,depth) in enumerate(profiles):
 for i in range(segments):
  t=2*pi*i/segments;fold=.009*cos(17*t+.4*j)+.004*cos(29*t-j)
  x=(.239*r+fold)*cos(t);y=(.292*r+fold)*sin(t)
  # A soft peaked crown, not a circular helmet.
  y+=.034*max(0,sin(t))**8
  v.append(tuple(face((x,y,depth+.006*cos(11*t)))))
for j in range(len(profiles)-1):
 for i in range(segments):a=j*segments+i;b=j*segments+(i+1)%segments;f.append((a,b,b+segments,a+segments))
hood=mesh('Sculpted hollow hood and rolled edge',v,f,0,'Head')
for p in hood.data.polygons:
 if p.index<segments:p.material_index=1
ell('Deep hood interior',face((0,0,-.09)),(.218,.275,.03),3,'Head',R,32,16)

# Skull components: raised brow, recessed sockets, cheekbones, nose, open jaw.
ell('Skull dome',face((0,.10,-.005)),(.146,.148,.059),2,'Head',R,28,16)
ell('Upper maxilla',face((0,-.033,.01)),(.098,.057,.034),2,'Head',R)
for s in [-1,1]:
 ell('Hollow eye socket '+str(s),face((s*.076,.07,.049)),(.059,.052,.013),3,'Head',R)
 # Inward-slanting heavy eyebrows make the expression visible at game scale.
 tube('Bony brow '+str(s),[face((s*.02,.096,.065)),face((s*.075,.124,.065)),face((s*.131,.119,.038))],[.014,.022,.015],2,'Head',10)
 tube('Cheek arch '+str(s),[face((s*.128,.047,.021)),face((s*.137,-.012,.043)),face((s*.10,-.070,.041))],[.027,.026,.016],2,'Head',10)
 ell('Temple plane '+str(s),face((s*.143,.031,-.008)),(.029,.07,.04),2,'Head',R)
 # Thin icy slits, tucked under the brows rather than bulging spheres.
 vv=[tuple(face((s*x,y,.065))) for x,y in [(.033,.064),(.113,.086),(.108,.062),(.043,.044)]]
 mesh('Soul slit '+str(s),vv,[(0,1,2,3)],4,'Head')
mesh('Triangular nasal hollow',[tuple(face(p)) for p in [(0,.007,.052),(-.023,-.039,.048),(0,-.025,.054),(.023,-.039,.048)]],[(0,1,2),(0,2,3)],3,'Head')
ell('Open screaming mouth',face((0,-.142,.015)),(.093,.122,.023),3,'Head',R,28,16)
pts=[face((.11*cos(t),-.108+.145*sin(t),.031)) for t in [pi+pi*j/18 for j in range(19)]]
tube('Open jaw bone',pts,[.012+.003*sin(pi*j/18) for j in range(19)],2,'Head',8)
for row in [0,1]:
 for j in range(5):
  x=(j-2)*.03;y=-.062 if row==0 else -.235
  length=(.041 if row==0 else .027)*(1+.22*cos(j*2))
  end=y-length if row==0 else y+length
  mesh('Irregular tooth %d %d'%(row,j),[tuple(face((x-.011,y,.049))),tuple(face((x+.011,y,.049))),tuple(face((x+.004,end,.049))),tuple(face((x,y,.028)))],[(0,1,2),(0,3,1),(1,3,2),(2,3,0)],2,'Head')

# Deeply folded sleeves and elevated, curled skeletal hands.
for s,label in [(-1,'L'),(1,'R')]:
 bone='Sleeve.'+label
 tube('Upper arm under sleeve '+label,[(s*.20,0,.87),(s*.29,-.075,.89),(s*.36,-.22,.96)],[.105,.094,.058],1,bone,16)
 v=[];f=[];n=40;rows=15
 for j in range(rows+1):
  t=j/rows
  for i in range(n):
   a=2*pi*i/n;r=(.104*(1-.32*t)+.009*cos(12*a+2*t))
   z=.87-.40*t+.055*sin(a*5+.8)*t**4
   v.append((s*(.285+.07*t)+r*cos(a),-.075+.12*t+r*.8*sin(a),z))
 for j in range(rows):
  for i in range(n):a=j*n+i;b=j*n+(i+1)%n;f.append((a,b,b+n,a+n))
 mesh('Ragged hanging sleeve '+label,v,f,0,bone)
 # Several long free cloth fingers at the sleeve hem.
 for k in range(4):
  a=2*pi*k/4
  tube('Sleeve torn strip '+label+str(k),[(s*.35+.075*cos(a),.045+.065*sin(a),.51),(s*.36+.062*cos(a),.12+.061*sin(a),.40),(s*.37+.04*cos(a),.16+.043*sin(a),.34)],[.018,.012,.001],0,bone,5)
 palm=Vector((s*.378,-.278,1.0))
 ell('Bony palm '+label,palm,(.067,.077,.032),2,bone,seg=20,rings=10)
 tube('Wrist '+label,[(s*.33,-.18,.93),(s*.36,-.23,.976),palm],[.045,.038,.041],2,bone,12)
 for k in range(4):
  xx=s*(.378+(k-1.5)*.032);length=[.117,.15,.146,.105][k]
  start=Vector((xx,-.319,1.005));endx=xx+s*(k-1.5)*.012
  pts=[start,Vector((endx,-.35,1.043)),Vector((endx,-.35-length*.48,1.075)),Vector((endx,-.35-length*.87,1.064)),Vector((endx,-.35-length,1.023))]
  tube('Curled finger '+label+str(k),pts,[.018,.017,.015,.012,.0065],2,bone,8)
  ell('Knuckle '+label+str(k),pts[1],(.02,.021,.017),2,bone,seg=12,rings=6)
  tube('Hand tendon '+label+str(k),[palm+Vector((s*(k-1.5)*.017,.03,.027)),start+Vector((0,0,.029)),pts[1]],[.005,.007,.005],0,bone,6)
 tube('Opposing thumb '+label,[palm+Vector((-s*.043,0,0)),palm+Vector((-s*.091,-.016,.025)),palm+Vector((-s*.11,-.055,.045)),palm+Vector((-s*.097,-.084,.025))],[.023,.02,.016,.007],2,bone,9)

# Torn cowl ribbons framing the skull and reaching into the upper torso.
for s in [-1,1]:
 for k in range(3):
  p0=face((s*(.21+.018*k),-.095-k*.04,-.002))
  tube('Cowl hanging fold '+str(s)+str(k),[p0,Vector((s*(.23+.018*k),-.13,.87)),Vector((s*(.23+.016*k),-.045,.65)),Vector((s*(.22+.012*k),.015,.55-k*.035))],[.014,.018,.012,.001],0,'Hover',7)


