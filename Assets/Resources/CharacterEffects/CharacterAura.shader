Shader "MazeBoo/CharacterAura"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (0.3,0.8,1,1)
        _Strength("Strength", Float) = 1
        _Opacity("Opacity", Range(0, 2)) = 0.684
        _Spread("Spread", Range(1, 5)) = 1.368
        _Softness("Softness", Range(0.5, 6)) = 2.2
        [HideInInspector] _FloorY("Floor height", Float) = 0
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
                float _Opacity;
                float _Spread;
                float _Softness;
                float _FloorY;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 center = TransformObjectToWorld(float3(0,0,0));
                float3 bodySize = float3(length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                // A flat pool of light just under the body: the body always covers it,
                // and it never dips into the floor where the depth test would cut it.
                // Slide it along the view ray so it stays centred behind the body
                // under the board camera's tilt.
                float floorY = _FloorY;
                float3 camera = GetCameraPositionWS();
                float3 ray = center - camera;
                float3 pool = camera + ray * clamp((floorY - camera.y) / min(ray.y, -1e-4), 1, 1.5);
                float extent = max(bodySize.x, bodySize.z) * _Spread;
                float3 world = pool + float3(input.positionOS.x, 0, input.positionOS.y) * extent;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2 - 1);
                // Soft light that is brightest against the body and fades outward,
                // with no edge that could read as a shield bubble.
                float falloff = pow(saturate(1 - radius), _Softness);
                float core = pow(saturate(1 - radius * 1.6), 2);
                float glow = falloff + core * 0.5;
                return half4(_Tint.rgb, glow * _Opacity * _Strength * _Tint.a);
            }
            ENDHLSL
        }
    }
}
