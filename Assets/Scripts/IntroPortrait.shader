Shader "PacGhost/Portrait"
{
    Properties { _MainTex ("Character texture", 2D) = "white" {} _Tint ("Tint", Color) = (1,0.64,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; float4 _Tint;
            CBUFFER_END
            V vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
            half4 frag(V i):SV_Target
            {
                half3 n=normalize(i.normalWS);
                half light=.68+.32*saturate(dot(n,normalize(float3(-.5,.8,.7))));
                half3 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;
                return half4(tex*_Tint.rgb*light,1);
            }
            ENDHLSL
        }
    }
}
