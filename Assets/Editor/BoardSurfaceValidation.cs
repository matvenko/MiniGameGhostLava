using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BoardSurfaceValidation
{
    public static string Run()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Use Play Mode for board transition validation.");
        Time.timeScale=0;
        var manager=Object.FindFirstObjectByType<LevelManager>();
        var liquid=Object.FindFirstObjectByType<LiquidSurface>();
        var walls=Object.FindFirstObjectByType<WallSurface>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var level=typeof(LevelManager).GetField("_level",flags);
        var layout=typeof(LevelManager).GetMethod("ApplyLevelLayout",flags);
        int original=(int)level.GetValue(manager);
        var report=new StringBuilder();
        try
        {
            foreach(int target in new[]{1,2,5,6,8,1})
            {
                level.SetValue(manager,target);layout.Invoke(manager,null);
                bool meadow=target<6;
                var surface=liquid.transform.Find("LiquidSurface").GetComponent<MeshRenderer>();
                // Old surfaces are disabled immediately and destroyed at frame end.
                foreach(var candidate in liquid.GetComponentsInChildren<MeshRenderer>())
                    if(candidate.name=="LiquidSurface")surface=candidate;
                var props=new MaterialPropertyBlock();surface.GetPropertyBlock(props);
                var mask=props.GetTexture("_ShoreMap") as Texture2D;
                var rect=props.GetVector("_BoardRect");
                int banks=0;foreach(var mr in liquid.GetComponentsInChildren<MeshRenderer>())if(mr.name=="Meadow bank grass")banks++;
                if(meadow && (mask==null||mask.width!=(int)rect.z||mask.height!=(int)rect.w||banks!=1))throw new Exception("Invalid meadow shoreline on level "+target);
                if(!meadow && (mask!=null||banks!=0))throw new Exception("Meadow shoreline leaked into cave "+target);
                if(meadow)CheckMask(mask,rect);
                var wallMesh=walls.GetComponentInChildren<MeshFilter>().sharedMesh;
                if((wallMesh.name=="Forest staggered masonry")!=meadow)throw new Exception("Wrong wall geometry on level "+target);
                foreach(var filter in liquid.GetComponentsInChildren<MeshFilter>())CheckMesh(filter.sharedMesh);
                CheckMesh(wallMesh);
                if(liquid.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Decorative liquid geometry added colliders.");
                report.AppendLine($"PASS level {target}: {(meadow?"shore mask matches every grass/water cell; one grass bank mesh; bevelled masonry":"cave restored; no meadow mask or grass banks")}; meshes valid.");
            }
        }
        finally{level.SetValue(manager,original);layout.Invoke(manager,null);}
        Directory.CreateDirectory("Art/Forest");File.WriteAllText("Art/Forest/board-validation.txt",report.ToString());return report.ToString();
    }

    static void CheckMask(Texture2D source,Vector4 rect)
    {
        var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var previous=RenderTexture.active;var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();
            foreach(string parent in new[]{"Blocks","Lava"})foreach(Transform tile in GameObject.Find(parent).transform)
            {
                int x=Mathf.FloorToInt(tile.position.x-rect.x),z=Mathf.FloorToInt(tile.position.z-rect.y);
                bool land=copy.GetPixel(x,z).r>.5f;if(land!=(parent=="Blocks"))throw new Exception("Shoreline mask does not match tile "+tile.position);
            }
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(copy);}
    }

    static void CheckMesh(Mesh mesh)
    {
        if(mesh==null||mesh.vertexCount==0)throw new Exception("Empty generated mesh.");
        foreach(var v in mesh.vertices)if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.sqrMagnitude))throw new Exception("Nonfinite vertex.");
        foreach(int index in mesh.triangles)if(index<0||index>=mesh.vertexCount)throw new Exception("Invalid triangle index.");
    }
}
