Shader "MazeBoo/CoinGlint"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (4,3.2,1.6,1)
        _Strength("Strength", Float) = 1
        _RayWidth("Ray width", Range(0.01, 0.2)) = 0.1
        _Core("Core size", Range(0.02, 0.5)) = 0.24
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Strength;
                float _RayWidth;
                float _Core;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                // Always faces the camera: a glint is a point of light on the
                // metal, not something lying on the board.
                float3 center = TransformObjectToWorld(float3(0,0,0));
                float size = length(unity_ObjectToWorld._m00_m10_m20);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 world = center + (right * input.positionOS.x + up * input.positionOS.y) * size;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                // Four thin rays that taper to a point, plus a small hot core:
                // a hard sparkle, the kind polished gold throws, not a soft cloud.
                float along = saturate(1 - r);
                float rays = saturate(1 - abs(p.x) / (_RayWidth * along + 1e-4)) * along
                           + saturate(1 - abs(p.y) / (_RayWidth * along + 1e-4)) * along;
                float core = pow(saturate(1 - r / _Core), 1.5);
                float glint = saturate(rays + core);
                return half4(_Tint.rgb, glint * _Strength * _Tint.a);
            }
            ENDHLSL
        }
    }
}
