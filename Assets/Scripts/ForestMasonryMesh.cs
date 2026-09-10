using System.Collections.Generic;
using UnityEngine;

// A single decorative mesh of staggered, bevelled stones. Gameplay keeps the
// original wall colliders; the silhouette and joints belong to this mesh only.
public static class ForestMasonryMesh
{
    public static Mesh Build(HashSet<Vector2Int> cells, Vector3 origin, Vector2 cell, float top, float height, float thickness)
    {
        var low=new Vector2Int(int.MaxValue,int.MaxValue);var high=new Vector2Int(int.MinValue,int.MinValue);
        foreach(var c in cells){low=Vector2Int.Min(low,c);high=Vector2Int.Max(high,c);}
        float width=Mathf.Min(cell.x,cell.y)*thickness;
        float minX=origin.x+low.x*cell.x+cell.x*.5f-width;
        float maxX=origin.x+high.x*cell.x-cell.x*.5f+width;
        float minZ=origin.z+low.y*cell.y+cell.y*.5f-width;
        float maxZ=origin.z+high.y*cell.y-cell.y*.5f+width;
        var builder=new Builder();var random=new System.Random(173);
        for(int row=0;row<4;row++)
        {
            float h=row==3?.22f:(height-.22f)/3;
            float y=row==3?top-h*.5f:top-height+row*h+h*.5f;
            float w=row==3?width+.06f:width;
            builder.Run(minX,maxX,minZ+width*.5f,y,w,h,true,row,random);
            builder.Run(minX,maxX,maxZ-width*.5f,y,w,h,true,row,random);
            builder.Run(minZ+width,maxZ-width,minX+width*.5f,y,w,h,false,row,random);
            builder.Run(minZ+width,maxZ-width,maxX-width*.5f,y,w,h,false,row,random);
        }
        return builder.Finish();
    }

    sealed class Builder
    {
        readonly List<Vector3> vertices=new List<Vector3>(), normals=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();
        public void Run(float start,float end,float cross,float y,float width,float height,bool alongX,int row,System.Random random)
        {
            float p=start;
            while(p<end-.01f)
            {
                float length=Mathf.Min(end-p,(p==start && (row&1)==1)?.64f:1.05f+(float)random.NextDouble()*.65f);
                if(end-p-length<.3f)length=end-p;
                float tone=(float)random.NextDouble();
                Vector3 center=alongX?new Vector3(p+length*.5f,y,cross):new Vector3(cross,y,p+length*.5f);
                Vector3 size=alongX?new Vector3(length-.025f,height-.022f,width):new Vector3(width,height-.022f,length-.025f);
                Stone(center,size,Mathf.Min(.075f,height*.25f),new Color(tone,row==3?1:.25f,0,1));p+=length;
            }
        }

        void Stone(Vector3 center,Vector3 size,float bevel,Color color)
        {
            Vector3 half=size*.5f,inner=half-Vector3.one*bevel;
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                Vector3 normal=Vector3.zero;normal[axis]=sign;
                Vector3 u=axis==1?Vector3.right:Vector3.Cross(Vector3.up,normal);
                Vector3 v=Vector3.Cross(normal,u);
                float hu=Vector3.Dot(half,new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)));
                float hv=Vector3.Dot(half,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));
                float[] us={-hu,-hu+bevel,hu-bevel,hu},vs={-hv,-hv+bevel,hv-bevel,hv};
                int first=vertices.Count;
                for(int j=0;j<4;j++)for(int i=0;i<4;i++)
                {
                    Vector3 p=normal*half[axis]+u*us[i]+v*vs[j];
                    Vector3 q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    Vector3 n=(p-q).normalized;vertices.Add(center+q+n*bevel);normals.Add(n);colors.Add(color);
                }
                for(int j=0;j<3;j++)for(int i=0;i<3;i++)
                {int a=first+j*4+i;triangles.AddRange(new[]{a,a+1,a+5,a,a+5,a+4});}
            }
        }

        public Mesh Finish()
        {
            var mesh=new Mesh{name="Forest staggered masonry",hideFlags=HideFlags.DontSave};
            if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
