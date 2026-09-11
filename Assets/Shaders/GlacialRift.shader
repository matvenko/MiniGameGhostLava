Shader "MazeBoo/Glacial Rift Water"
{
 Properties
 {
  _MeadowShore("Use board shoreline map",Float)=1
  _IceWater("Suppress grass banks",Float)=1
  _ShoreMap("Land mask",2D)="black"{}
  _BoardRect("Board rectangle",Vector)=(0,0,30,24)
  _ShoreEnabled("Shore enabled",Float)=0
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
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_ShoreMap);SAMPLER(sampler_ShoreMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BoardRect;float _MeadowShore,_IceWater,_ShoreEnabled;
   CBUFFER_END
   struct A{float4 p:POSITION;};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.fog=ComputeFogFactor(o.p.z);return o;}
   float land(float2 cell)
   {
    if(any(cell<0)||any(cell>=_BoardRect.zw))return 1;
    return SAMPLE_TEXTURE2D(_ShoreMap,sampler_ShoreMap,(cell+.5)/_BoardRect.zw).r;
   }
   half4 frag(V i):SV_Target
   {
    float2 p=i.w.xz,board=p-_BoardRect.xy,cell=floor(board),f=frac(board);
    float d=2;
    if(_ShoreEnabled>.5)
    {
     d=min(d,lerp(2,f.x,land(cell+float2(-1,0))));
     d=min(d,lerp(2,1-f.x,land(cell+float2(1,0))));
     d=min(d,lerp(2,f.y,land(cell+float2(0,-1))));
     d=min(d,lerp(2,1-f.y,land(cell+float2(0,1))));
    }
    float flow=sin(p.x*3.5+p.y*1.7+sin(p.y*2-_Time.y*.24))*.5+.5;
    float ripple=pow(saturate(sin(p.x*6-p.y*4+flow*2+_Time.y*.38)),18);
    half3 col=lerp(half3(.018,.045,.09),half3(.035,.14,.20),flow*.55);
    col+=half3(.025,.075,.095)*ripple;
    float edge=(1-smoothstep(.02,.24,d));
    col=lerp(col,half3(.12,.36,.43),edge*.7);
    float frost=1-smoothstep(.018,.06+sin(p.x*23+p.y*17)*.014,d);
    col=lerp(col,half3(.48,.72,.80),frost*.85);
    return half4(MixFog(col,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Unlit/DepthOnly"
 }
}
