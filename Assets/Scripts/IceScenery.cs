using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Two batched meshes; no physics objects, dynamic lights, or per-frame rebuilds.
// Everything sits beyond the playable footprint. Low foreground silhouettes
// keep the intro camera from looking through a curtain of crystal spires.
internal sealed class IceScenery
{
    readonly GameObject root;
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Vector3>[] vertices = {new List<Vector3>(),new List<Vector3>()};
    readonly List<int>[] triangles = {new List<int>(),new List<int>()};
    readonly System.Random random;
    readonly float x,z;
    public IceScenery(Transform parent,Vector2 centre,Vector2 size,float y,int seed,Material snow,Material crystal)
    {
        random=new System.Random(seed+1100);
        x=size.x*.5f+.9f;z=size.y*.5f+.9f;
        root=new GameObject("Ice cavern (generated)"){hideFlags=HideFlags.DontSave};
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(centre.x,y,centre.y);
        Quad(0,P(-x-42,-z-42),P(-x-42,-z),P(x+42,-z),P(x+42,-z-42));
        Quad(0,P(-x-42,z),P(-x-42,z+42),P(x+42,z+42),P(x+42,z));
        Quad(0,P(-x-42,-z),P(-x-42,z),P(-x,z),P(-x,-z));
        Quad(0,P(x,-z),P(x,z),P(x+42,z),P(x+42,-z));
        for(int i=0;i<100;i++)
        {
            Vector3 p=Perimeter(R(2.3f,18));
            float radius=R(.6f,1.7f);
            Spire(0,p,radius,R(.16f,.55f),Quaternion.Euler(0,R(0,360),0));
        }
        for(int i=0;i<42;i++)
        {
            Vector3 p=Perimeter(R(2.6f,15));
            float height=p.z>z?R(.4f,1.0f):R(1.1f,3.4f);
            Quaternion turn=Quaternion.Euler(R(-12,12),R(0,360),R(-12,12));
            Spire(1,p,R(.3f,.6f),height,turn);
            Spire(1,p+new Vector3(.55f,0,.2f),.22f,height*.55f,Quaternion.Euler(0,30,-22));
            Spire(1,p+new Vector3(-.4f,0,.3f),.18f,height*.38f,Quaternion.Euler(18,0,14));
            Spire(0,p+Vector3.down*.025f,.85f,.16f,Quaternion.identity);
        }
        // A few large rear formations frame the intro without blocking the field.
        for(float px=-x-8;px<=x+8;px+=5.5f)
        {
            Spire(1,P(px,-z-13),R(1.1f,1.8f),R(3.2f,5.3f),Quaternion.Euler(0,R(0,360),R(-8,8)));
        }
        // Small hanging icicles along the outside face of the existing rampart.
        // Their tips remain above the ground and never add a collision surface.
        var wallSurface=Object.FindFirstObjectByType<WallSurface>();
        float crest=y+1f, edgeX=x-.15f, edgeZ=z-.15f;
        if(wallSurface!=null)
            foreach(var r in wallSurface.GetComponentsInChildren<MeshRenderer>())
            { crest=r.bounds.max.y; edgeX=r.bounds.extents.x-.04f; edgeZ=r.bounds.extents.z-.04f; }
        for(int side=0;side<4;side++)
        {
            float span=side<2?x:z;
            for(float t=-span+.7f;t<span-.7f;t+=1.1f)
            {
                Vector3 p=side==0?P(t,-edgeZ):side==1?P(t,edgeZ):side==2?P(-edgeX,t):P(edgeX,t);
                p.y=crest-y-.03f;
                Spire(1,p,R(.075f,.14f),R(.3f,.75f),Quaternion.Euler(180,0,0));
            }
        }
        Material[] materials={snow,crystal};
        for(int i=0;i<2;i++)
        {
            var mesh=new Mesh{name=i==0?"Snow shelves":"Faceted glacial crystals",hideFlags=HideFlags.DontSave,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices[i]);mesh.SetTriangles(triangles[i],0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer)){hideFlags=HideFlags.DontSave};
            go.transform.SetParent(root.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=materials[i];renderer.shadowCastingMode=ShadowCastingMode.On;
        }
    }
    static Vector3 P(float x,float z)=>new Vector3(x,0,z);
    float R(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    Vector3 Perimeter(float d)
    {
        switch(random.Next(4)){case 0:return P(R(-x,x),-z-d);case 1:return P(R(-x,x),z+d);case 2:return P(-x-d,R(-z,z));default:return P(x+d,R(-z,z));}
    }
    void Tri(int m,Vector3 a,Vector3 b,Vector3 c)
    {
        int start=vertices[m].Count;vertices[m].Add(a);vertices[m].Add(b);vertices[m].Add(c);
        triangles[m].Add(start);triangles[m].Add(start+1);triangles[m].Add(start+2);
    }
    void Quad(int m,Vector3 a,Vector3 b,Vector3 c,Vector3 d){Tri(m,a,b,c);Tri(m,a,c,d);}
    void Spire(int m,Vector3 p,float radius,float height,Quaternion turn)
    {
        Vector3 tip=p+turn*new Vector3(radius*.15f,height,0);
        for(int i=0;i<6;i++)
        {
            float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;
            Vector3 lowA=p+turn*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
            Vector3 lowB=p+turn*new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
            Vector3 highA=p+turn*new Vector3(Mathf.Cos(a)*radius*.82f,height*.7f,Mathf.Sin(a)*radius*.82f);
            Vector3 highB=p+turn*new Vector3(Mathf.Cos(b)*radius*.82f,height*.7f,Mathf.Sin(b)*radius*.82f);
            Quad(m,lowB,lowA,highA,highB);Tri(m,highB,highA,tip);
        }
    }
    public void Dispose()
    {
        if(root!=null){root.SetActive(false);Release(root);}
        foreach(var mesh in meshes)if(mesh!=null)Release(mesh);
        meshes.Clear();
    }
    static void Release(Object o){if(Application.isPlaying)Object.Destroy(o);else Object.DestroyImmediate(o);}
}
