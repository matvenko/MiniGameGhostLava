// Low-poly ground: every facet takes a flat colour from a shared palette, with
// hand-painted leaf detail modulating its brightness.
//
// Which palette band a facet gets is still decided by GroundSurface.cs and baked
// into the vertex colours; this shader only ramps, jitters, textures and lights.
// That split is deliberate: two earlier attempts generated the surface here in
// HLSL, where the noise driving it could not be measured, and both shipped bugs
// that were invisible until rendered - once a photographic texture that would not
// match the stylised water, once a procedural one whose noise never left its
// middle band. On the CPU the distribution can be printed and checked before
// anything is drawn.
//
// _DetailMap does not break that rule, because it carries no colour of its own.
// It was high-passed against its own local mean before import, so it is a ratio
// around 0.5 - pure light and shade with the green divided out. It can only
// modulate the band the CPU chose, never override it.
//
// Vertex colour layout (written by GroundSurface.cs):
//   r = palette band 0..1, flat across the triangle
//   g = shoreline proximity 0..1, 1 at the water's edge
//   b = per-facet brightness jitter 0..1
//   a = 1 on top faces, 0 on the skirt walls
Shader "Custom/LowPolyGround_URP"
{
    Properties
    {
        [Header(Grass Palette)]
        _GrassDeep ("Grass Deep", Color) = (0.13, 0.40, 0.16, 1)
        _GrassMid ("Grass Mid", Color) = (0.27, 0.60, 0.21, 1)
        _GrassLight ("Grass Light", Color) = (0.50, 0.82, 0.31, 1)
        _FacetJitter ("Facet Jitter", Range(0, 0.3)) = 0.07

        [Header(Bank)]
        _SoilDark ("Soil Dark", Color) = (0.26, 0.20, 0.14, 1)
        _SoilLight ("Soil Light", Color) = (0.44, 0.35, 0.23, 1)
        _ShoreWidth ("Shoreline Width", Range(0,1)) = 0.35
        _ShoreAmount ("Shoreline Soil", Range(0,1)) = 0.35

        [Header(Painted Detail)]
        [NoScaleOffset] _DetailMap ("Meadow Detail (linear, 0.5 neutral)", 2D) = "linearGrey" {}
        _DetailScale ("Detail Tiles Per Unit", Float) = 0.5
        _DetailScaleB ("Second Layer Tiles Per Unit", Float) = 0.309
        _DetailBlend ("Second Layer Mix", Range(0,1)) = 0.45
        _DetailStrength ("Detail Strength", Range(0,1.5)) = 0.55
        _DetailOnSoil ("Detail On Bank", Range(0,1)) = 0.45

        [Header(Flowers)]
        _FlowerWhite ("Flower White", Color) = (0.96, 0.97, 0.92, 1)
        _FlowerYellow ("Flower Yellow", Color) = (0.98, 0.86, 0.35, 1)
        _FlowerDensity ("Flowers Per World Unit", Float) = 2.6
        _FlowerSize ("Flower Size", Range(0.02, 0.4)) = 0.13
        _FlowerChance ("Flower Chance", Range(0, 1)) = 0.22

        [Header(Lighting)]
        _FacetShading ("Flatten Facet Lighting", Range(0,1)) = 0.75
        _LightSteps ("Light Bands", Range(1, 6)) = 3
        _CelStrength ("Banding Strength", Range(0,1)) = 0.8
        _AmbientColor ("Ambient", Color) = (0.32, 0.38, 0.42, 1)
        _RimColor ("Rim", Color) = (0.60, 0.94, 0.78, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.1

        [HideInInspector] _BaseMap ("Unused", 2D) = "white" {}
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        // Shared by every pass so the UnityPerMaterial layout stays identical -
        // SRP batching silently drops the shader otherwise. SurfaceInput.hlsl
        // supplies _BaseMap and the helpers the stock URP shadow and depth passes
        // expect to find already declared.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Common/ArtStyle.hlsl"

        // Outside the cbuffer on purpose - a texture handle in UnityPerMaterial
        // breaks the SRP batcher's layout match and the shader silently drops out
        // of batching.
        TEXTURE2D(_DetailMap);
        SAMPLER(sampler_DetailMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _GrassDeep;
            float4 _GrassMid;
            float4 _GrassLight;
            float  _FacetJitter;
            float  _DetailScale;
            float  _DetailScaleB;
            float  _DetailBlend;
            float  _DetailStrength;
            float  _DetailOnSoil;
            float4 _SoilDark;
            float4 _SoilLight;
            float  _ShoreWidth;
            float  _ShoreAmount;
            float4 _FlowerWhite;
            float4 _FlowerYellow;
            float  _FlowerDensity;
            float  _FlowerSize;
            float  _FlowerChance;
            float  _FacetShading;
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
                float3 pw = IN.positionWS;

                half zone   = IN.color.r;
                half shore  = IN.color.g;
                half jitter = IN.color.b;
                half isTop  = IN.color.a;

                half3 grass = ArtRamp3(_GrassDeep.rgb, _GrassMid.rgb, _GrassLight.rgb, zone);
                half3 soil  = lerp(_SoilDark.rgb, _SoilLight.rgb, zone);

                // Hand-painted leaf work, lifted from the reference sheet and
                // stored as a ratio around 0.5 - it carries no colour of its own,
                // so it modulates the palette band instead of replacing it. That
                // keeps the rule this whole scene is built on: the CPU picks the
                // colour, the shader only shades it.
                //
                // Sampled twice at scales that share no common period, and the
                // second layer rotated off-axis. One layer at this board size
                // repeats visibly under a top-down camera; two that never line up
                // average the repeat away for one extra fetch.
                float2x2 kDetailRot = float2x2(0.8, -0.6, 0.6, 0.8);
                half3 devA = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, pw.xz * _DetailScale).rgb - 0.5h;
                half3 devB = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, mul(kDetailRot, pw.xz) * _DetailScaleB).rgb - 0.5h;

                // Weights renormalised by rsqrt, not a plain lerp. The two layers
                // are uncorrelated, so a straight average drops their combined
                // contrast to ~0.71 of one layer and _DetailStrength stops meaning
                // what it says. This keeps the mix independent of _DetailBlend.
                half wA = 1.0h - _DetailBlend;
                half wB = _DetailBlend;
                half3 dev = (devA * wA + devB * wB) * rsqrt(max(wA * wA + wB * wB, 1e-4h));

                // Flat on the skirt: those walls are vertical, and an XZ-projected
                // sample smears into streaks down them.
                half3 detailMul = lerp(1.0h, 1.0h + dev * 2.0h * _DetailStrength, isTop);
                grass *= detailMul;
                soil  *= lerp(half3(1, 1, 1), detailMul, _DetailOnSoil);

                // Flower heads on a jittered grid. No patch mask on purpose: every
                // patch-based version clumped them all into one corner of the board.
                float2 cellUV = pw.xz * _FlowerDensity;
                float2 cellId = floor(cellUV);
                float2 cellPos = frac(cellUV) - 0.5;
                half seed = ArtHash21(cellId);
                float2 offset = float2(ArtHash21(cellId + 7.13), ArtHash21(cellId + 3.71)) - 0.5;

                half head = 1.0h - smoothstep(_FlowerSize * 0.7h, _FlowerSize,
                    length(cellPos - offset * 0.7));
                half3 petal = seed < _FlowerChance * 0.4h ? _FlowerYellow.rgb : _FlowerWhite.rgb;
                grass = lerp(grass, petal, saturate(head * step(seed, _FlowerChance)) * isTop);

                half shoreMask = smoothstep(1.0h - _ShoreWidth, 1.0h, shore) * _ShoreAmount;
                half3 albedo = lerp(soil, lerp(grass, soil, shoreMask), isTop);

                // Equal bands would read as one flat mass; this is what keeps the
                // individual facets legible.
                albedo *= 1.0h + (jitter - 0.5h) * 2.0h * _FacetJitter;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                    half atten = mainLight.shadowAttenuation;
                #else
                    Light mainLight = GetMainLight();
                    half atten = 1.0h;
                #endif

                float3 faceAxis = isTop > 0.5h ? float3(0, 1, 0) : normalize(float3(N.x, 0, N.z));
                float3 litN = ArtFacetNormal(N, faceAxis, _FacetShading);
                half lit = ArtCelLight(litN, mainLight.direction, _LightSteps, _CelStrength);

                half3 color = albedo * (mainLight.color * lit * atten + _AmbientColor.rgb);

                // Light rim on the tiles facing away, so each island reads against
                // the bright water instead of dissolving into it.
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

        // The water's depth fade samples the camera depth texture, so the ground
        // has to write into it or the shoreline breaks.
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
