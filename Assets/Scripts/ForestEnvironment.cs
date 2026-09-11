using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A decorative ring, rebuilt only when the board changes. Nothing is placed on
// playable cells, and the mesh has an actual hole underneath the board/water.
[ExecuteAlways]
public class ForestEnvironment : MonoBehaviour
{
    public GameObject[] trees;
    public GameObject[] bushes;
    public GameObject[] details;
    public Material groundMaterial;
    public int seed = 7139;
    public int lastForestLevel = 5;
    [SerializeField] private Vector2 previewCentre = new Vector2(2.02f, 1f);
    [SerializeField] private Vector2 previewSize = new Vector2(30, 20);
    [SerializeField] private float groundY = .32f;
    private GameObject generated;
    private Mesh groundMesh;
    private System.Random random;
    private BurntScenery burnt;
    [Header("Ice cavern (levels 11–16)")]
    public Material snowMaterial;
    public Material crystalMaterial;
    private IceScenery ice;

    void OnEnable()
    {
        if (!Application.isPlaying) Rebuild(previewCentre, previewSize, 1);
    }

    void OnDisable() => Clear();

    public void Rebuild(Vector2 centre, Vector2 size, int level)
    {
        Clear();
        if (level >= 11 && level <= 16)
        {
            if (snowMaterial != null && crystalMaterial != null)
                ice = new IceScenery(transform, centre, size, groundY, seed, snowMaterial, crystalMaterial);
            return;
        }
        if (level >= 6 && level <= 10)
        {
            burnt = new BurntScenery(transform, centre, size, groundY, seed);
            return;
        }
        if (level > lastForestLevel || groundMaterial == null || trees == null || trees.Length == 0) return;
        previewCentre = centre;
        previewSize = size;
        random = new System.Random(seed);
        generated = new GameObject("Forest scenery (generated)");
        generated.hideFlags = HideFlags.DontSave;
        generated.transform.SetParent(transform, false);
        generated.transform.position = new Vector3(centre.x, groundY, centre.y);
        float x = size.x * .5f + .9f, z = size.y * .5f + .9f;
        BuildGround(x, z);

        // The overview looks from +Z. Keep its foreground low; a canopy there
        // would project across the board even if the trunk were outside it.
        for (int row = 0; row < 3; row++)
        {
            float depth = 4.8f + row * 5.5f;
            for (float px = -x - 13; px <= x + 13; px += 4.8f)
                Place(trees, new Vector3(px + Range(-1.4f, 1.4f), 0, -z - depth + Range(-1, 1)), Range(.72f, 1.12f), "Canopy");
            for (int side = -1; side <= 1; side += 2)
                for (float pz = -z; pz < z + 5; pz += 4.8f)
                    Place(trees, new Vector3(side * (x + depth + Range(-.7f, .7f)), 0, pz + Range(-1, 1)), Range(.68f, 1.05f), "Woodland");
        }
        // Understory follows all four walls, hiding the hard transition to the
        // surrounding soil without changing the wall colliders or crest.
        for (int i = 0; i < 110; i++)
        {
            Vector3 p = Perimeter(x, z, Range(.7f, 5.5f));
            Place(bushes, p, Range(.38f, .85f), "Understory");
        }
        for (int i = 0; i < 390; i++)
        {
            Vector3 p = Perimeter(x, z, Range(.25f, 5.8f));
            Place(details, p, Range(.65f, 1.5f), "Forest floor");
        }
    }

    Vector3 Perimeter(float x, float z, float distance)
    {
        switch (random.Next(4))
        {
            case 0: return new Vector3(Range(-x, x), 0, -z - distance);
            case 1: return new Vector3(Range(-x, x), 0, z + distance);
            case 2: return new Vector3(-x - distance, 0, Range(-z, z));
            default: return new Vector3(x + distance, 0, Range(-z, z));
        }
    }

    void Place(GameObject[] choices, Vector3 position, float scale, string label)
    {
        if (choices == null || choices.Length == 0) return;
        // Stumps are accents, not an even share of the flower/fern scatter.
        int choice = random.Next(choices.Length);
        if (choices == details && choice == choices.Length - 1 && random.Next(8) != 0)
            choice = random.Next(choices.Length - 1);
        GameObject prefab = choices[choice];
        if (prefab == null) return;
        var go = Instantiate(prefab, generated.transform);
        go.name = label + " / " + prefab.name;
        go.hideFlags = HideFlags.DontSave;
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0, Range(0, 360), 0);
        go.transform.localScale *= scale;
    }

    float Range(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());

    void BuildGround(float x, float z)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        // Four trapezoids meet without overlap. No invisible floor covers the
        // liquid hazards, including after a level grows to its maximum size.
        Vector3[] inner = {new Vector3(-x,0,-z),new Vector3(-x,0,z),new Vector3(x,0,z),new Vector3(x,0,-z)};
        Vector3[] outer = {new Vector3(-x-42,0,-z-42),new Vector3(-x-42,0,z+42),new Vector3(x+42,0,z+42),new Vector3(x+42,0,-z-42)};
        for (int side = 0; side < 4; side++)
        {
            int next = (side+1)%4, start = vertices.Count;
            foreach(var p in new[]{inner[side],outer[side],outer[next],inner[next]})
            { vertices.Add(p); uv.Add(new Vector2(p.x/5f,p.z/5f)); }
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        groundMesh = new Mesh {name="Forest clearing ring", hideFlags=HideFlags.DontSave};
        groundMesh.SetVertices(vertices); groundMesh.SetUVs(0,uv); groundMesh.SetTriangles(triangles,0); groundMesh.RecalculateNormals(); groundMesh.RecalculateBounds();
        var floor = new GameObject("Forest soil",typeof(MeshFilter),typeof(MeshRenderer));
        floor.hideFlags=HideFlags.DontSave;
        floor.transform.SetParent(generated.transform,false);
        floor.GetComponent<MeshFilter>().sharedMesh=groundMesh;
        var renderer=floor.GetComponent<MeshRenderer>(); renderer.sharedMaterial=groundMaterial;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    void Clear()
    {
        if (burnt != null) { burnt.Dispose(); burnt = null; }
        if (ice != null) { ice.Dispose(); ice = null; }
        if (generated != null) { generated.SetActive(false); Release(generated); }
        if (groundMesh != null) Release(groundMesh);
        generated=null; groundMesh=null;
    }

    void Release(Object obj) { if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
}

// Decorative geometry is batched by material and never receives colliders.
// Uses a private random stream so scenery cannot change gameplay generation.
internal sealed class BurntScenery
{
    readonly GameObject root;
    readonly List<Object> owned = new List<Object>();
    readonly System.Random rng;
    readonly List<Vector3>[] verts = new List<Vector3>[5];
    readonly List<int>[] indices = new List<int>[5];
    readonly float x, z;

    public BurntScenery(Transform parent, Vector2 centre, Vector2 size, float y, int seed)
    {
        rng = new System.Random(seed + 600);
        x = size.x * .5f + .9f; z = size.y * .5f + .9f;
        root = new GameObject("Burnt wasteland (generated)");
        root.hideFlags = HideFlags.DontSave;
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(centre.x, y, centre.y);
        for (int i = 0; i < 5; i++) { verts[i] = new List<Vector3>(); indices[i] = new List<int>(); }

        // Four strips leave the complete board footprint open, including hazards.
        Quad(0, V(-x-42,-z-42), V(-x-42,-z), V(x+42,-z), V(x+42,-z-42));
        Quad(0, V(-x-42,z), V(-x-42,z+42), V(x+42,z+42), V(x+42,z));
        Quad(0, V(-x-42,-z), V(-x-42,z), V(-x,z), V(-x,-z));
        Quad(0, V(x,-z), V(x,z), V(x+42,z), V(x+42,-z));

        // Ash plates and broken basalt interrupt the bare soil, with small gaps
        // between facets suggesting parched, fractured ground.
        for (int i = 0; i < 620; i++)
        {
            Vector3 p = Perimeter(R(.7f, 30));
            Rock(i % 3 == 0 ? 1 : 0, p, R(.35f, 1.7f), R(.025f, .11f));
        }
        for (int i = 0; i < 85; i++)
        {
            Vector3 p = Perimeter(R(2.2f, 16));
            float h = p.z > z ? R(.25f,.7f) : R(.65f,2.6f);
            Rock(2, p, R(.25f,.65f), h);
            Rock(2, p + new Vector3(.55f,0,.3f), .23f, h*.45f);
        }
        // Broken walls are low in the camera foreground, taller at the rear.
        for (int i = 0; i < 19; i++)
        {
            Vector3 p = Perimeter(R(3.5f, 11));
            Quaternion turn = Quaternion.Euler(0, R(0,360), 0);
            int rows = p.z > z ? 1 : rng.Next(2,4);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < 4-row; col++)
                {
                    Vector3 offset = turn * new Vector3((col-1.5f)*.72f + row*.28f,row*.43f,0);
                    Box(p+offset, new Vector3(.67f,.39f,.48f), turn);
                }
        }
        // Each stream stays on one exterior side; its furthest inward edge is
        // more than two units beyond the board border.
        for (int side = 0; side < 4; side++)
        {
            float length = (side < 2 ? x : z) + 25;
            for (int branch = 0; branch < 2; branch++)
            {
                float phase = R(0,6.28f);
                for (float t = -length; t < length; t += 1.2f)
                {
                    float next = Mathf.Min(length,t+1.2f);
                    Vector3 a = Stream(side,t,branch,phase), b = Stream(side,next,branch,phase);
                    Vector3 normal = Vector3.Cross(Vector3.up,(b-a).normalized);
                    float width = branch == 0 ? .48f : .23f;
                    Ribbon(2,a,b,normal,width+.22f,.12f);
                    Ribbon(3,a,b,normal,width,.135f);
                    Ribbon(4,a,b,normal,width*.24f,.14f);
                }
            }
        }
        Color[] colours = {new Color(.105f,.078f,.065f),new Color(.25f,.225f,.21f),new Color(.045f,.035f,.065f),new Color(1,.12f,.008f),new Color(1,.57f,.06f)};
        string[] names = {"Scorched earth", "Ash and charcoal masonry", "Obsidian shards", "Lava streams", "Molten cores"};
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) { Dispose(); return; }
        for (int i = 0; i < 5; i++)
        {
            var material = new Material(shader) {name=names[i],hideFlags=HideFlags.DontSave};
            material.SetColor("_BaseColor",colours[i]); material.SetColor("_Color",colours[i]);
            material.SetFloat("_Smoothness",i==2?.72f:.12f);
            if (i >= 3) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",colours[i]*(i==4?2.5f:1.5f)); }
            owned.Add(material);
            var mesh = new Mesh {name=names[i],hideFlags=HideFlags.DontSave,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(verts[i]); mesh.SetTriangles(indices[i],0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            owned.Add(mesh);
            var go = new GameObject(names[i],typeof(MeshFilter),typeof(MeshRenderer));
            go.hideFlags=HideFlags.DontSave; go.transform.SetParent(root.transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=i>=3?ShadowCastingMode.Off:ShadowCastingMode.On;
        }
    }
    float R(float a,float b) => Mathf.Lerp(a,b,(float)rng.NextDouble());
    static Vector3 V(float a,float b) => new Vector3(a,0,b);
    Vector3 Perimeter(float distance)
    {
        switch(rng.Next(4))
        {
            case 0:return V(R(-x,x),-z-distance);
            case 1:return V(R(-x,x),z+distance);
            case 2:return V(-x-distance,R(-z,z));
            default:return V(x+distance,R(-z,z));
        }
    }
    Vector3 Stream(int side,float t,int branch,float phase)
    {
        float d=5+branch*11+Mathf.Sin(t*.19f+phase)*1.4f+Mathf.Sin(t*.47f+phase)*.5f;
        switch(side) {case 0:return V(t,-z-d);case 1:return V(t,z+d);case 2:return V(-x-d,t);default:return V(x+d,t);}
    }
    void Ribbon(int m,Vector3 a,Vector3 b,Vector3 n,float width,float height)
    {
        a.y=b.y=height;
        Quad(m,a-n*width,b-n*width,b+n*width,a+n*width);
    }
    void Tri(int m,Vector3 a,Vector3 b,Vector3 c)
    {
        int first=verts[m].Count; verts[m].Add(a);verts[m].Add(b);verts[m].Add(c);
        indices[m].Add(first);indices[m].Add(first+1);indices[m].Add(first+2);
    }
    void Quad(int m,Vector3 a,Vector3 b,Vector3 c,Vector3 d) {Tri(m,a,b,c);Tri(m,a,c,d);}
    void Rock(int m,Vector3 p,float radius,float height)
    {
        Vector3 top=p+new Vector3(radius*.24f,height,-radius*.18f);
        for(int i=0;i<5;i++)
        {
            float a=i*Mathf.PI*2/5,b=(i+1)*Mathf.PI*2/5;
            Tri(m,p+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),p+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),top);
        }
    }
    void Box(Vector3 p,Vector3 size,Quaternion rotation)
    {
        Vector3[] v=new Vector3[8];
        for(int i=0;i<8;i++) v[i]=p+rotation*new Vector3((i%2-.5f)*size.x,((i/2)%2)*size.y,((i/4)-.5f)*size.z);
        Quad(1,v[0],v[2],v[3],v[1]); Quad(1,v[4],v[5],v[7],v[6]);
        Quad(1,v[0],v[4],v[6],v[2]); Quad(1,v[1],v[3],v[7],v[5]); Quad(1,v[2],v[6],v[7],v[3]);
    }
    public void Dispose()
    {
        if(root!=null) {root.SetActive(false);Release(root);}
        foreach(var item in owned) if(item!=null) Release(item);
        owned.Clear();
    }
    static void Release(Object item) {if(Application.isPlaying) Object.Destroy(item);else Object.DestroyImmediate(item);}
}
