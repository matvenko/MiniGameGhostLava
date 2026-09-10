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

    void OnEnable()
    {
        if (!Application.isPlaying) Rebuild(previewCentre, previewSize, 1);
    }

    void OnDisable() => Clear();

    public void Rebuild(Vector2 centre, Vector2 size, int level)
    {
        Clear();
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
        if (generated != null) { generated.SetActive(false); Release(generated); }
        if (groundMesh != null) Release(groundMesh);
        generated=null; groundMesh=null;
    }

    void Release(Object obj) { if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
}
