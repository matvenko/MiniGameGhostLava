using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ForestSceneReview
{
    public static string ValidatePlayMode()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Requires play mode.");
        Time.timeScale=0;
        var manager=Object.FindFirstObjectByType<LevelManager>();
        var forest=Object.FindFirstObjectByType<ForestEnvironment>();
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var levelField=typeof(LevelManager).GetField("_level",flags);
        var layout=typeof(LevelManager).GetMethod("ApplyLevelLayout",flags);
        int original=(int)levelField.GetValue(manager);
        var report=new List<string>();
        try
        {
            foreach(int level in new[]{1,5,6,8,1})
            {
                levelField.SetValue(manager,level);layout.Invoke(manager,null);
                int colliders=forest.GetComponentsInChildren<Collider>().Length;
                int renderers=forest.GetComponentsInChildren<Renderer>().Length;
                int invalid=0,inside=0;
                var walls=GameObject.Find("Walls");Bounds bounds=new Bounds();bool first=true;
                foreach(Transform wall in walls.transform)
                {if(first){bounds=new Bounds(wall.position,Vector3.one);first=false;}else bounds.Encapsulate(new Bounds(wall.position,Vector3.one));}
                foreach(var renderer in forest.GetComponentsInChildren<Renderer>())
                {
                    foreach(var material in renderer.sharedMaterials)if(material==null||material.shader==null||!material.shader.isSupported)invalid++;
                    if(renderer.GetComponent<MeshFilter>()?.sharedMesh==null)invalid++;
                }
                foreach(Transform root in forest.transform)
                    if(root.gameObject.activeSelf)foreach(Transform decoration in root)
                    {
                        if(decoration.name=="Forest soil")continue;
                        var p=decoration.position;
                        if(p.x>bounds.min.x+.2f&&p.x<bounds.max.x-.2f&&p.z>bounds.min.z+.2f&&p.z<bounds.max.z-.2f)inside++;
                    }
                bool expected=level<=forest.lastForestLevel;
                if(colliders!=0||invalid!=0||inside!=0||(renderers>0)!=expected)
                    throw new System.InvalidOperationException($"Level {level}: colliders={colliders}, invalid={invalid}, inside={inside}, renderers={renderers}");
                report.Add($"PASS level {level}: wall footprint {bounds.size.x} x {bounds.size.z}; forest renderers {renderers}; no invalid meshes/materials, gameplay colliders or scenery origins inside walls.");
            }
        }
        finally{levelField.SetValue(manager,original);layout.Invoke(manager,null);}
        string text=string.Join("\n",report);Directory.CreateDirectory("Art/Forest");File.WriteAllText("Art/Forest/validation.txt",text);return text;
    }

    // Camera.Render does not refresh LOD selection after teleporting a camera
    // within one editor tick. Force the preview LOD temporarily, then restore.
    public static void Capture(string path, bool overview = true)
    {
        var camera=GameObject.Find("Camera").GetComponent<Camera>();
        var forest=Object.FindFirstObjectByType<ForestEnvironment>();
        var groups=forest.GetComponentsInChildren<LODGroup>();
        var pos=camera.transform.position;var rot=camera.transform.rotation;
        var target=camera.targetTexture;float aspect=camera.aspect;
        var previous=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24);
        var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            foreach(var group in groups)group.ForceLOD(0);
            if(overview)
            {
                Bounds bounds=new Bounds();bool first=true;
                foreach(Transform tile in GameObject.Find("Walls").transform)
                {if(first){bounds=new Bounds(tile.position,Vector3.one);first=false;}else bounds.Encapsulate(new Bounds(tile.position,Vector3.one));}
                camera.transform.rotation=Quaternion.Euler(55,180,0);
                camera.transform.position=new Vector3(bounds.center.x,.4f,bounds.center.z)-camera.transform.forward*Mathf.Max(bounds.size.x*.90f,bounds.size.z*1.25f);
            }
            camera.aspect=16f/9;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally
        {
            camera.transform.SetPositionAndRotation(pos,rot);camera.targetTexture=target;camera.aspect=aspect;
            RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);
            foreach(var group in groups)if(group!=null)group.ForceLOD(-1);
        }
        Debug.Log("Saved actual Unity camera render: "+path);
    }
}
