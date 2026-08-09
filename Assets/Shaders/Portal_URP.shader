// URP port combining the "Force Field" pack's fresnel rim glow with the
// "Plasma" shader's animated noise swirl, repurposed as a flat summoning
// portal disc (both source shaders are Built-in RP surface/CG shaders and
// render pink under URP, same issue as LavaFlow_URP).
Shader "Custom/Portal_URP"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (0.4, 0.8, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0, 10)) = 3
        _SwirlSpeed ("Swirl Speed", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
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
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            float4 _Color;
            float _FresnelPower;
            float _SwirlSpeed;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vpi.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(IN.viewDirWS);
                float3 normal = normalize(IN.normalWS);
                float rim = 1.0 - saturate(dot(viewDir, normal));
                float fresnel = pow(rim, _FresnelPower);

                // swirl: rotate the noise UVs around the disc center over time
                float2 center = float2(0.5, 0.5);
                float2 dir = IN.uv - center;
                float angle = _Time.y * _SwirlSpeed;
                float s = sin(angle);
                float c = cos(angle);
                float2 rotatedUV = float2(dir.x * c - dir.y * s, dir.x * s + dir.y * c) + center;

                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, rotatedUV);
                half4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float dist = distance(IN.uv, center) * 2.0;
                float radialFade = saturate(1.0 - dist);

                half3 color = _Color.rgb * baseTex.rgb * (noise.r * 0.7 + fresnel * 0.6 + 0.15);
                half alpha = saturate(noise.r * radialFade * 1.3 + fresnel * 0.4) * _Color.a;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
