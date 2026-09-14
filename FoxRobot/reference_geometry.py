# Reference image coordinates are used only to lay out genuine volumetric meshes.
# Blender XY is the board plane. Z remains depth, with no billboard or image plane.
def xy(px, py): return ((px-630)/480, (700-py)/480)

# Wide face nested into the shell, aimed towards the player/camera.
UP=Vector((0,cos(math.radians(17)),sin(math.radians(17))))
NORMAL=Vector((0,-sin(math.radians(17)),cos(math.radians(17))))
C=Vector((0,-.148,.736))
def fp(x,y,depth):return C+Vector((x,0,0))+UP*y+NORMAL*depth

# A single watertight shell with an actual front opening. The cream chin is
# part of this surface, not an intersecting sphere pasted onto the body.
BC=C-NORMAL*(.365*cos(.75))
n=128;rows=72;vs=[];fs=[]
for j in range(rows):
    theta=.75+(pi-.75)*j/rows
    front_compression=1-.13*math.exp(-max(0,theta-.75)/.40)
    for i in range(n):
        a=2*pi*i/n
        vs.append(tuple(BC+Vector((.438*sin(theta)*cos(a),0,0))+UP*(.438*sin(theta)*sin(a)*front_compression)+NORMAL*(.365*cos(theta))+Vector((0,.05*sin(pi*(theta-.75)/(pi-.75)),0))))
for j in range(rows-1):
    for i in range(n):
        a=j*n+i;b=j*n+(i+1)%n;fs.append((a,a+n,b+n,b))
end=len(vs);vs.append(tuple(BC-NORMAL*.365))
for i in range(n):fs.append(((rows-1)*n+i,end,(rows-1)*n+(i+1)%n))
body=mesh('Continuous coral and ivory housing',vs,fs,coral)
body.data.materials.append(cream)
for polygon in body.data.polygons:
    row=polygon.index//n;angle=polygon.index%n
    polygon.material_index=1 if row<29 and 72<=angle<120 else 0

def face_surface(name,rx,ry,depth,bow,mat):
    n=96; rings=24
    vs=[tuple(fp(0,0,depth+bow))];fs=[]
    for j in range(1,rings+1):
        r=j/rings
        for i in range(n):
            a=2*pi*i/n;vs.append(tuple(fp(rx*r*cos(a),ry*r*sin(a),depth+bow*(1-r*r))))
    fs.extend((0,1+i,1+(i+1)%n) for i in range(n))
    for j in range(rings-1):
        for i in range(n):
            a=1+j*n+i;b=1+j*n+(i+1)%n;fs.append((a,a+n,b+n,b))
    return mesh(name,vs,fs,mat)

def face_ring(name,rx,ry,width,height,depth,mat):
    vs=[];fs=[];n=128;m=16
    for i in range(n):
        a=2*pi*i/n
        for j in range(m):
            b=2*pi*j/m
            vs.append(tuple(fp((rx+width*cos(b))*cos(a),(ry+width*cos(b))*sin(a),depth+height*sin(b))))
    for i in range(n):
        for j in range(m):fs.append((i*m+j,((i+1)%n)*m+j,((i+1)%n)*m+(j+1)%m,i*m+(j+1)%m))
    return mesh(name,vs,fs,mat)

face_ring('Bezel recessed gasket',.300,.258,.022,.022,.002,shadow)
face_ring('Sculpted ivory face surround',.297,.256,.028,.027,.016,cream)
face_ring('Cyan luminous inner rim',.273,.232,.0045,.0045,.027,cyan)
face_surface('Convex black glass',.271,.230,.027,.025,screen)

def face_z(x,y):return .027+.025*(1-(x/.271)**2-(y/.230)**2)+.003
for sign,label in [(-1,'L'),(1,'R')]:
    # The concept has plump oval pixels, not thin slits.
    x=sign*.120;y=-.014
    n=40;vs=[tuple(fp(x,y,face_z(x,y)))];fs=[]
    for i in range(n):
        a=2*pi*i/n;ex=x+.037*cos(a);ey=y+.064*sin(a)
        vs.append(tuple(fp(ex,ey,face_z(ex,ey))))
    fs=[(0,i+1,(i+1)%n+1) for i in range(n)]
    mesh('Digital oval eye '+label,vs,fs,cyan,'Eye.'+label)
for i,(px,py) in enumerate([(-.043,-.098),(-.032,-.108),(-.019,-.114),(0,-.114),(.019,-.114),(.032,-.108),(.043,-.098)]):
    d=.004
    mesh('Smile pixel %02d'%i,[tuple(fp(x,y,face_z(x,y))) for x,y in [(px-d,py-d),(px+d,py-d),(px+d,py+d),(px-d,py+d)]],[(0,1,2,3)],cyan,'Smile')

# Soft triangular ears: rounded contours, inflated coral lip and recessed inserts.
def rounded_triangle(points,cut=.20,steps=16):
    out=[]
    for i,p in enumerate(points):
        p=Vector(p);prev=Vector(points[(i-1)%3]);nxt=Vector(points[(i+1)%3])
        a=p+(prev-p)*cut;b=p+(nxt-p)*cut
        for j in range(steps):
            t=j/steps;out.append((1-t)**2*a+2*t*(1-t)*p+t*t*b)
        end=nxt+(p-nxt)*cut
        for j in range(steps):out.append(b.lerp(end,j/steps))
    # Ensure the winding faces up in Blender.
    if sum(out[i].x*out[(i+1)%len(out)].y-out[(i+1)%len(out)].x*out[i].y for i in range(len(out)))<0:out.reverse()
    return out

def pillow(name,outline,z,bulge,mat,bone):
    center=sum(outline,Vector((0,0)))/len(outline);n=len(outline);vs=[(center.x,center.y,z+bulge)];fs=[]
    for j in range(1,13):
        t=j/12
        for p in outline:
            q=center.lerp(p,t);vs.append((q.x,q.y,z+bulge*(1-t*t)))
    fs.extend((0,i+1,(i+1)%n+1) for i in range(n))
    for j in range(11):
        for i in range(n):
            a=1+j*n+i;b=1+j*n+(i+1)%n;fs.append((a,a+n,b+n,b))
    back=len(vs);vs.append((center.x,center.y,z-.045))
    fs.extend((back,1+11*n+(i+1)%n,1+11*n+i) for i in range(n))
    return mesh(name,vs,fs,mat,bone)

for sign,label in [(-1,'L'),(1,'R')]:
    def ex(points):
        return [(sign*abs(xy(x,y)[0]),xy(x,y)[1]) for x,y in points]
    outer=rounded_triangle(ex([(450,493),(589,563),(457,666)]),.32)
    inset=rounded_triangle(ex([(477,548),(561,592),(461,651)]),.15)
    # Interpolate rings for a rounded frame, with the rim closest to the camera.
    n=len(outer);vs=[];fs=[]
    for j in range(17):
        t=(1-cos(pi*j/16))/2
        for a,b in zip(outer,inset):
            p=a.lerp(b,t);z=.813+.062*sin(pi*j/16)+.008*t
            vs.append((p.x,p.y,z))
    for j in range(16):
        for i in range(n):
            a=j*n+i;b=j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    mesh('Soft coral ear frame '+label,vs,fs,coral,'Ear.'+label)
    # Close the coral frame with a genuinely thick rounded side wall.
    sidevs=[];sidefs=[]
    center=sum(outer,Vector((0,0)))/len(outer)
    for j in range(9):
        t=j/8
        for p in outer:
            q=center+(p-center)*(1-.10*t)
            sidevs.append((q.x,q.y+.014*t,.813-.13*t))
    for j in range(8):
        for i in range(n):
            a=j*n+i;b=j*n+(i+1)%n;sidefs.append((a,a+n,b+n,b))
    sidefs.append(tuple(reversed(range(8*n,9*n))))
    mesh('Volumetric ear back '+label,sidevs,sidefs,coral,'Ear.'+label)
    pillow('Deep teal ear recess '+label,inset,.816,.012,inner,'Ear.'+label)
    panel=rounded_triangle(ex([(479,561),(515,602),(469,637)]),.19)
    pillow('Inset ivory ear panel '+label,panel,.839,.014,cream,'Ear.'+label)
    for part in parts:
        if part.get('bone')=='Ear.'+label:
            for vertex in part.data.vertices:vertex.co.z+=.65*(vertex.co.y-.25)
    # A single rounded paw shell with an inset cream cap on its front hemisphere.
    center=Vector((sign*.422,-.171,.48))
    paw=ell('Integrated paw '+label,(0,0,0),(.094,.111,.102),coral,'Paw.'+label,64,40)
    paw.data.materials.append(cream)
    for polygon in paw.data.polygons:polygon.material_index=1 if polygon.center.z>.032 else 0
    for vertex in paw.data.vertices:
        p=vertex.co.copy();vertex.co=center+Vector((p.x,0,0))+UP*p.y+NORMAL*p.z
    signal=ell('Inset paw signal '+label,(0,0,0),(.012,.041,.006),cyan,'Paw.'+label,24,16)
    for vertex in signal.data.vertices:
        p=vertex.co.copy();vertex.co=center+Vector((p.x,0,0))+UP*p.y+NORMAL*(p.z+.103)

# The tail is a plump curved leaf, its tip and seams follow the supplied image.
profile=[(.025,.332,.39,.099,.087),(.032,.443,.44,.150,.120),(.064,.555,.48,.191,.138),(.111,.691,.50,.183,.131),(.166,.815,.51,.131,.097),(.230,.909,.52,.051,.040),(.258,.920,.52,.003,.004)]
def sample_tail(t):
    i=min(int(t),len(profile)-2);u=t-i
    a,b,c,d=[profile[max(0,min(len(profile)-1,j))] for j in (i-1,i,i+1,i+2)]
    return [0.5*(2*b[k]+(-a[k]+c[k])*u+(2*a[k]-5*b[k]+4*c[k]-d[k])*u*u+(-a[k]+3*b[k]-3*c[k]+d[k])*u*u*u) for k in range(5)]
rows=96;sides=64;vs=[];fs=[]
for j in range(rows+1):
    t=(len(profile)-1)*j/rows
    for i in range(sides):
        a=2*pi*i/sides
        warp=(.18*cos(a)-.45*sin(a))*math.exp(-((t-3.75)/1.2)**2)*t*(6-t)/(3.75*2.25)
        x,y,z,rx,rz=sample_tail(t+warp)
        vs.append((x+rx*cos(a),y,z+rz*sin(a)))
for j in range(rows):
    for i in range(sides):
        a=j*sides+i;b=j*sides+(i+1)%sides;fs.append((a,a+sides,b+sides,b))
fs.extend([tuple(reversed(range(sides))),tuple(rows*sides+i for i in range(sides))])
tail=mesh('Sculpted segmented fox tail',vs,fs,coral,'Tail.1')
tail.data.materials.append(cream)
for polygon in tail.data.polygons:
    p=polygon.center
    polygon.material_index=1 if polygon.index//sides>=60 and polygon.index<rows*sides else 0
# Fine dark panel gaps follow the curved tail, with tiny raised coral lips.
for t,name in [(1.28,'base'),(3.75,'tip')]:
    pts=[]
    for i in range(81):
        a=2*pi*i/80
        tt=t+(.18*cos(a)-.45*sin(a) if name=='tip' else .45*sin(a))
        x,y,z,rx,rz=sample_tail(tt)
        pts.append((x+(rx+.001)*cos(a),y,z+(rz+.001)*sin(a)))
    tube('Tail panel seam '+name,pts,.003,shadow,'Tail.1')
pts=[]
for j in range(30):
    t=1.78+1.50*j/29;x,y,z,rx,rz=sample_tail(t)
    a=pi/2+.12*sin(pi*j/29);pts.append((x+rx*cos(a),y,z+rz*sin(a)+.002))
tube('Tail longitudinal panel channel',pts,.004,shadow,'Tail.1')
tube('Tail cyan status light',pts[10:17],.0045,cyan,'Tail.1')

# Panel seam lies on the shell itself, including the cream lower housing.
pts=[];theta=.75+(pi-.75)*29/72
for i in range(129):
    a=2*pi*i/128
    p=BC+Vector((.4395*sin(theta)*cos(a),0,0))+UP*(.4395*sin(theta)*sin(a)*(1-.13*math.exp(-(theta-.75)/.40)))+NORMAL*(.3665*cos(theta))+Vector((0,.05*sin(pi*(theta-.75)/(pi-.75)),0))
    pts.append(tuple(p))
tube('Housing panel seam',pts,.0018,shadow)
tube('Crown cyan notch',pts[31:34],.003,cyan)

