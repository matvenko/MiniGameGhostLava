Shader "MazeBoo/Ice Cavern Surface"
{
 Properties
 {
  _BaseColor("Glacial blue",Color)=(.16,.32,.43,1)
  _SnowColor("Fresh frost",Color)=(.66,.79,.85,1)
  _SnowCoverage("Snow coverage",Range(0,1))=.38
  _Crystal("Crystal sheen",Range(0,1))=0
  _Masonry("Bevelled wall",Float)=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor,_SnowColor;float _SnowCoverage,_Crystal,_Masonry;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half fog:TEXCOORD2;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.fog=ComputeFogFactor(o.p.z);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y);}
   half4 frag(V i):SV_Target
   {
    float3 n=normalize(i.n);float2 p=i.w.xz;
    // Sparse irregular cell boundaries look like hairline fractures, not tile seams.
    float2 q=p*.72,cell=floor(q),f=frac(q);float nearest=8,second=8;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
    {
     float2 offset=float2(x,y),id=cell+offset;
     float2 delta=offset+float2(hash(id),hash(id+17.7))-f;
     float d=dot(delta,delta);
     if(d<nearest){second=nearest;nearest=d;}else second=min(second,d);
    }
    float cracks=1-smoothstep(.012,.055,second-nearest);
    float cloud=noise(p*.27)+noise(p*1.8)*.20;
    float snow=smoothstep(1-_SnowCoverage,1.24-_SnowCoverage,cloud)*smoothstep(.35,.9,n.y);
    float grain=noise(p*42);
    half3 base=_BaseColor.rgb*(.88+noise(p*.9)*.22);
    base=lerp(base,base*.48,cracks*.44*(1-_Crystal));
    base=lerp(base,_SnowColor.rgb*(.93+grain*.07),snow);
    // Vertical glacial striations and frost caps on the bevelled border blocks.
    float veins=pow(saturate(sin(i.w.y*8+noise(i.w.xz*.8)*8)),14);
    base+=_BaseColor.rgb*veins*.14*(1-saturate(n.y));
    Light light=GetMainLight(TransformWorldToShadowCoord(i.w));
    float diffuse=saturate(dot(n,light.direction)*.65+.35);
    float3 view=GetWorldSpaceNormalizeViewDir(i.w);
    float rim=pow(1-saturate(dot(n,view)),3);
    float spec=pow(saturate(dot(n,normalize(light.direction+view))),64)*(.12+_Crystal*.55)*(1-snow);
    half3 col=base*(half3(.53,.62,.73)+light.color*diffuse*.56*lerp(.55,1,light.shadowAttenuation));
    col+=half3(.19,.46,.57)*rim*(.13+_Crystal*.55)+spec*light.color;
    return half4(MixFog(col,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
