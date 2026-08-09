// URP port of the "Lava3D" flowing-lava effect (original is a Built-in
// Render Pipeline surface shader and renders pink under URP). Same idea:
// pan the UVs over time for flow, displace vertices by a height map for a
// bubbling surface, and add an emissive glow so lava reads as self-lit.
Shader "Custom/LavaFlow_URP"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _HeightMap ("Height Map (grayscale)", 2D) = "black" {}
        [HDR] _Color ("Color Tint", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission Tint", Color) = (1,1,1,1)
        _FlowDirection ("Flow Direction (xy)", Vector) = (1,0,0,0)
        _Speed ("Flow Speed", Float) = 0.1
        _Amplitude ("Wave Amplitude", Float) = 0.03
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);

            float4 _MainTex_ST;
            float4 _Color;
            float4 _EmissionColor;
            float4 _FlowDirection;
            float _Speed;
            float _Amplitude;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float2 flowUV = IN.uv + _FlowDirection.xy * fmod(_Time.y, 1200.0) * _Speed;
                float height = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, flowUV, 0).r;

                float3 posOS = IN.positionOS.xyz;
                posOS.y += height * _Amplitude;

                OUT.positionHCS = TransformObjectToHClip(posOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 flowUV = IN.uv + _FlowDirection.xy * fmod(_Time.y, 1200.0) * _Speed;
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUV) * _Color;
                half4 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, flowUV) * _EmissionColor;
                half3 color = albedo.rgb + emission.rgb;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
