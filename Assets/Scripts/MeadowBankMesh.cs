using System.Collections.Generic;
using UnityEngine;

// Small shoreline blades, merged once per level and entirely non-colliding.
public static class MeadowBankMesh
{
    public static Mesh Build(Color[] land,int width,int height,Vector2 origin,float y)
    {
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
        var random=new System.Random(4821);
        Vector2Int[] directions={Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
        for(int z=0;z<height;z++)for(int x=0;x<width;x++)
        {
            if(land[z*width+x].r<.5f)continue;
            foreach(var d in directions)
            {
                int nx=x+d.x,nz=z+d.y;
                if(nx<0||nx>=width||nz<0||nz>=height||land[nz*width+nx].r>.5f)continue;
                var outward=new Vector3(d.x,0,d.y);var along=new Vector3(-d.y,0,d.x);
                Vector3 center=new Vector3(origin.x+x+.5f,y,origin.y+z+.5f)+outward*.465f;
                for(int tuft=0;tuft<4;tuft++)
                {
                    Vector3 start=center+along*(-.38f+tuft*.25f+(float)random.NextDouble()*.055f);
                    for(int blade=0;blade<3;blade++)
                    {
                        Vector3 p=start+along*(blade-1)*.024f;
                        float h=.065f+(float)random.NextDouble()*.075f;
                        Vector3 tip=p+Vector3.up*h+outward*(.035f+(float)random.NextDouble()*.065f)+along*(blade-1)*.022f;
                        int first=vertices.Count;vertices.Add(p-along*.018f);vertices.Add(tip);vertices.Add(p+along*.018f);
                        normals.Add(Vector3.up);normals.Add(Vector3.up);normals.Add(Vector3.up);
                        triangles.AddRange(new[]{first,first+1,first+2});
                    }
                }
            }
        }
        var mesh=new Mesh{name="Meadow shoreline grass",hideFlags=HideFlags.DontSave};
        if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
    }
}
