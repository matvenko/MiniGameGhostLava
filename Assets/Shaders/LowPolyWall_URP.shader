// The border wall, in the same visual language as the ground: one flat palette
// colour per face, banded lighting, no texture.
//
// It shares ArtStyle.hlsl with the ground rather than duplicating the lighting,
// which is the whole point - the previous wall used a triplanar photo of stone
// and read as a different world from everything around it.
//
// Vertex colour layout (written by WallSurface.cs):
//   r = palette band 0..1, flat across the face
//   b = per-face brightness jitter 0..1
//   a = 1 on the crest, 0 on the vertical faces
Shader "Custom/LowPolyWall_URP"
{
    Properties
    {
        [Header(Stone Palette)]
        _StoneDark ("Stone Dark", Color) = (0.13, 0.13, 0.16, 1)
        _StoneMid ("Stone Mid", Color) = (0.22, 0.22, 0.25, 1)
        _StoneLight ("Stone Light", Color) = (0.34, 0.33, 0.35, 1)
        _CrestTint ("Crest Tint", Color) = (1.18, 1.15, 1.1, 1)
        _FacetJitter ("Face Jitter", Range(0, 0.3)) = 0.09

        [Header(Lighting)]
        _LightSteps ("Light Bands", Range(1, 6)) = 3
        _CelStrength ("Banding Strength", Range(0,1)) = 0.8
        _AmbientColor ("Ambient", Color) = (0.30, 0.34, 0.40, 1)
        _RimColor ("Rim", Color) = (0.45, 0.62, 0.75, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.12

        [HideInInspector] _BaseMap ("Unused", 2D) = "white" {}
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Common/ArtStyle.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _StoneDark;
            float4 _StoneMid;
            float4 _StoneLight;
            float4 _CrestTint;
            float  _FacetJitter;
            float  _LightSteps;
            float  _CelStrength;
            float4 _AmbientColor;
            float4 _RimColor;
            float  _RimPower;
            float  _RimStrength;
            float  _Cutoff;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 color       : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 N = normalize(IN.normalWS);

                half zone   = IN.color.r;
                half jitter = IN.color.b;
                half isTop  = IN.color.a;

                half3 albedo = ArtRamp3(_StoneDark.rgb, _StoneMid.rgb, _StoneLight.rgb, zone);
                // The crest catches the sky, so it stays lighter than the faces
                // below it even where the sun is not reaching it directly.
                albedo *= lerp(half3(1, 1, 1), _CrestTint.rgb, isTop);
                albedo *= 1.0h + (jitter - 0.5h) * 2.0h * _FacetJitter;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                    half atten = mainLight.shadowAttenuation;
                #else
                    Light mainLight = GetMainLight();
                    half atten = 1.0h;
                #endif

                // Box faces already have clean axis normals, so unlike the ground
                // there is nothing to flatten here.
                half lit = ArtCelLight(N, mainLight.direction, _LightSteps, _CelStrength);
                half3 color = albedo * (mainLight.color * lit * atten + _AmbientColor.rgb);

                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half rim = pow(1.0h - saturate(dot(N, viewDir)), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
