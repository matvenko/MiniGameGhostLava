Shader "MiniGame/Moonmilk"
{
    Properties { _BaseColor("Pearl color", Color) = (1,0.89,0.65,1) }
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
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS);
                half light=saturate(dot(n,normalize(float3(-.4,.85,.3)))*.5+.5);
                half rim=pow(1-saturate(dot(n,normalize(GetWorldSpaceViewDir(i.positionWS)))),3);
                return half4(_BaseColor.rgb*(.72+.30*light)+rim*half3(.18,.16,.10),1);
            }
            ENDHLSL
        }
    }
}
