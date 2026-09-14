Shader "MiniGame/FoxPorcelain"
{
 Properties {
  _BaseColor("Surface",Color)=(1,.5,.18,1)
  _ShadeColor("Shadow",Color)=(.35,.1,.025,1)
  [HDR] _EmissionColor("Lantern emission",Color)=(0,0,0,1)
  _Dissolve("Visibility",Range(0,1))=1
  _Gloss("Polished finish",Range(0,1))=.4
  _Glass("Glass face",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor,_ShadeColor,_EmissionColor;float _Dissolve,_Gloss,_Glass;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;half4 color:COLOR;};
   struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;float3 local:TEXCOORD2;half ao:TEXCOORD3;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.local=a.p.xyz;o.ao=a.color.r;return o;}
   half4 frag(V i):SV_Target{
    float grain=frac(sin(dot(floor(i.local*7000),float3(12.9898,78.233,37.719)))*43758.5453);
    clip(_Dissolve-.001-grain*.998);
    half3 n=normalize(i.n),v=normalize(GetWorldSpaceViewDir(i.w));
    half3 key=normalize(float3(-.45,.85,.5));
    half wrap=saturate(dot(n,key)*.5+.5);
    half3 h=normalize(key+v);
    half broad=pow(saturate(dot(n,h)),18)*.13;
    half spec=pow(saturate(dot(n,h)),lerp(35,110,_Gloss))*.45*_Gloss;
    half edge=pow(1-saturate(dot(n,v)),4);
    half3 color=lerp(_ShadeColor.rgb,_BaseColor.rgb,pow(wrap,1.8))+broad+spec+edge*.025;
    // A broad studio reflection makes the convex face read as dark polished glass.
    half reflection=pow(saturate(dot(n,normalize(float3(-.35,.92,-.2)))),18);
    half3 glass=_BaseColor.rgb+reflection*.075+spec*.3+edge*half3(.025,.045,.055);
    color=lerp(color*1.45*lerp(.38,1,saturate(i.ao)),glass,_Glass)+_EmissionColor.rgb;
    return half4(color,1);
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster"
   Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex shadowVert
   #pragma fragment shadowFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor,_ShadeColor,_EmissionColor;float _Dissolve,_Gloss,_Glass;
   CBUFFER_END
   float3 _LightDirection;float3 _LightPosition;
   struct A{float4 p:POSITION;float3 n:NORMAL;};
   struct V{float4 p:SV_POSITION;float3 local:TEXCOORD0;};
   V shadowVert(A a){
    V o;float3 w=TransformObjectToWorld(a.p.xyz);float3 n=TransformObjectToWorldNormal(a.n);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 light=normalize(_LightPosition-w);
    #else
    float3 light=_LightDirection;
    #endif
    o.p=TransformWorldToHClip(ApplyShadowBias(w,n,light));
    #if UNITY_REVERSED_Z
    o.p.z=min(o.p.z,UNITY_NEAR_CLIP_VALUE);
    #else
    o.p.z=max(o.p.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    o.local=a.p.xyz;return o;
   }
   half4 shadowFrag(V i):SV_Target{
    float grain=frac(sin(dot(floor(i.local*7000),float3(12.9898,78.233,37.719)))*43758.5453);
    clip(_Dissolve-.001-grain*.998);return 0;
   }
   ENDHLSL
  }
 }
}
