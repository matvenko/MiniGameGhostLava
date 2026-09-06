Shader "MiniGame/PinkSpirit"
{
 Properties
 {
  _MainTexture("Original face texture",2D)="white"{}
  _OcclusionTexture("Original occlusion",2D)="white"{}
  _MainColor("Rose",Color)=(1,.42,.65,1)
  _ShadowColor("Lavender shadow",Color)=(.37,.16,.42,1)
  _Dissolve("Visibility",Range(0,1))=1
  _DissolveColor("Dissolve edge",Color)=(1,.70,.85,1)
  _NoiseScale("Dissolve grain",Float)=70
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTexture);SAMPLER(sampler_MainTexture);
   TEXTURE2D(_OcclusionTexture);SAMPLER(sampler_OcclusionTexture);
   CBUFFER_START(UnityPerMaterial)
   half4 _MainColor,_ShadowColor,_DissolveColor;float _Dissolve,_NoiseScale;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;float3 local:TEXCOORD3;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;o.local=a.p.xyz;return o;}
   half4 frag(V i):SV_Target
   {
    float grain=frac(sin(dot(floor(i.local*_NoiseScale),float3(12.9898,78.233,37.719)))*43758.5453);
    clip(_Dissolve-.001-grain*.998);
    half3 n=normalize(i.n),v=normalize(GetWorldSpaceViewDir(i.w));
    half3 key=normalize(float3(-.45,.85,-.65));
    half wrap=saturate(dot(n,key)*.5+.5);
    half fresnel=pow(1-saturate(dot(n,v)),3);
    half gloss=pow(saturate(dot(n,normalize(key+v))),42);
    half soft=pow(saturate(dot(n,normalize(key+v))),8);
    half ao=SAMPLE_TEXTURE2D(_OcclusionTexture,sampler_OcclusionTexture,i.uv).r;
    half mask=smoothstep(.015,.15,SAMPLE_TEXTURE2D(_MainTexture,sampler_MainTexture,i.uv).r);
    half3 body=lerp(_ShadowColor.rgb,_MainColor.rgb,pow(wrap,.8))*(.85+.15*ao);
    body+=half3(1,.64,.76)*(fresnel*.23+soft*.09+gloss*.22);
    body+=_MainColor.rgb*(.035+.025*sin(_Time.y*2.1));
    // Original eye UV island; tiny oval reflections preserve its black eye design.
    float2 a=(i.uv-float2(.078,.151))/float2(.010,.014);
    float2 b=(i.uv-float2(.119,.112))/float2(.004,.006);
    half glint=(1-smoothstep(.55,1,length(a)))*.9+(1-smoothstep(.5,1,length(b)))*.5;
    half3 eye=half3(.008,.006,.018)+gloss*.24+glint*half3(.87,.94,1);
    half3 col=lerp(eye,body,mask);
    if(_Dissolve<.999)col+=_DissolveColor.rgb*(1-smoothstep(0,.055,_Dissolve-grain));
    return half4(col,1);
   }
   ENDHLSL
  }
 }
}
