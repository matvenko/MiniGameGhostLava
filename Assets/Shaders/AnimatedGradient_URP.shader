// Flowing multi-color gradient for menu/loading-screen backgrounds. Purely
// procedural (no textures), cheap, and easy to re-tune via 3 color fields.
Shader "Custom/AnimatedGradient_URP"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.10, 0.05, 0.25, 1)
        _ColorB ("Color B", Color) = (0.55, 0.10, 0.55, 1)
        _ColorC ("Color C", Color) = (0.05, 0.35, 0.45, 1)
        _Speed ("Flow Speed", Float) = 0.35
        _Scale ("Wave Scale", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
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

            float4 _ColorA;
            float4 _ColorB;
            float4 _ColorC;
            float _Speed;
            float _Scale;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float t = _Time.y * _Speed;
                float wave1 = sin((IN.uv.x + IN.uv.y) * _Scale + t) * 0.5 + 0.5;
                float wave2 = cos((IN.uv.x - IN.uv.y) * _Scale * 1.3 - t * 0.7) * 0.5 + 0.5;

                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, wave1);
                col = lerp(col, _ColorC.rgb, wave2 * 0.6);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
