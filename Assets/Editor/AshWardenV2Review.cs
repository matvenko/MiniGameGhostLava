using UnityEngine;
using UnityEditor;
using System.IO;
public static class AshWardenV2Review
{
 public static void Capture(string path)
 {
  var preview=new PreviewRenderUtility();Texture2D image=null;
  try
  {
   preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.31f,.29f,.27f);
   preview.camera.orthographic=true;preview.camera.orthographicSize=.78f;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=30;
   preview.camera.transform.position=new Vector3(-2,1.7f,-5);preview.camera.transform.LookAt(new Vector3(0,.59f,0));
   preview.lights[0].intensity=2;preview.lights[0].transform.rotation=Quaternion.Euler(45,30,0);
   preview.lights[1].intensity=1.2f;preview.lights[1].transform.rotation=Quaternion.Euler(30,-130,0);
   preview.ambientColor=new Color(.5f,.48f,.45f);
   var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/BurntLands/AshWardenV2/AshWarden_V2_Visual.prefab"));go.transform.rotation=Quaternion.Euler(0,180,0);preview.AddSingleGO(go);
   preview.BeginStaticPreview(new Rect(0,0,1400,1400));foreach(var filter in go.GetComponentsInChildren<MeshFilter>()) preview.DrawMesh(filter.sharedMesh,filter.transform.localToWorldMatrix,filter.GetComponent<MeshRenderer>().sharedMaterial,0);preview.Render(true);image=preview.EndStaticPreview();File.WriteAllBytes(path,image.EncodeToPNG());
  }
  finally {if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
 }
}

