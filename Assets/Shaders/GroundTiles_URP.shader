// Ground built from the painted pieces on ground2.png, one piece per board cell.
//
// This replaces LowPolyGround_URP's procedural palette. That shader generated
// grass - bands, flowers, a soil ramp - because there was no art to draw from.
// There is now: GroundTileSlicer cuts the sheet into square cell tiles and
// stacks them into a Texture2DArray, so the surface colour is the reference art
// itself and nothing here invents any of it.
//
// A texture array rather than an atlas: every cell picks its own tile, so tiles
// sit edge to edge all over the board. In an atlas that is exactly where
// bilinear taps and mip footprints cross into the neighbouring tile and bleed a
// different tile's grass through the seam. Array slices cannot touch.
//
// Lighting still goes through ArtStyle.hlsl, unchanged, so the ground responds
// to the day/night cycle identically to the wall and the water.
//
// Vertex data (written by GroundSurface.cs):
//   uv0.xy = position within the cell's tile, 0..1
//   uv0.z  = which slice of the tile array this cell draws
//   colour.g = shoreline proximity 0..1, 1 at the water's edge
//   colour.b = per-facet brightness jitter 0..1
//   colour.a = 1 on top faces, 0 on the skirt walls
Shader "Custom/GroundTiles_URP"
{
    Properties
    {
        [Header(Tiles)]
        [NoScaleOffset] _TileArray ("Ground Tile Array", 2DArray) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 2)) = 1.05
        _Contrast ("Contrast", Range(0.5, 2)) = 1.02

        [Header(Bank)]
        _SkirtColor ("Skirt Soil", Color) = (0.34, 0.26, 0.17, 1)
        _SkirtTexture ("Skirt Keeps Texture", Range(0, 1)) = 0.55
        _ShoreDamp ("Shoreline Darkening", Range(0, 1)) = 0.18

        [Header(Facets)]
        _FacetJitter ("Facet Jitter", Range(0, 0.3)) = 0.045

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

        // Same arrangement LowPolyGround_URP uses: one shared declaration block
        // so every pass sees an identical UnityPerMaterial layout, and
        // SurfaceInput.hlsl supplies what the stock shadow and depth passes
        // expect to already exist. Texture handles stay outside the cbuffer -
        // one inside breaks the SRP batcher's layout match.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Common/ArtStyle.hlsl"

        TEXTURE2D_ARRAY(_TileArray);
        SAMPLER(sampler_TileArray);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _Tint;
            float  _Saturation;
            float  _Contrast;
            float4 _SkirtColor;
            float  _SkirtTexture;
            float  _ShoreDamp;
            float  _FacetJitter;
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
            #pragma target 3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float3 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 uv          : TEXCOORD2;
                float4 color       : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
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
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 N = normalize(IN.normalWS);

                half shore  = IN.color.g;
                half jitter = IN.color.b;
                half isTop  = IN.color.a;

                // uv.z is flat across the triangle, so the interpolator hands
                // back the cell's slice exactly; rounding only guards against
                // the float landing a hair under the integer.
                half3 albedo = SAMPLE_TEXTURE2D_ARRAY(_TileArray, sampler_TileArray, IN.uv.xy, round(IN.uv.z)).rgb;

                half3 kLum = half3(0.2126h, 0.7152h, 0.0722h);

                // The skirt is the cut bank under the grass. It takes the tile's
                // light and dark only, never its hue: the UVs run vertically
                // down the wall, and grass sampled that way reads as grass hung
                // sideways rather than as exposed soil.
                half3 skirt = _SkirtColor.rgb * lerp(1.0h, 0.55h + dot(albedo, kLum), _SkirtTexture);
                albedo = lerp(skirt, albedo, isTop);

                half lum = dot(albedo, kLum);
                albedo = lerp(half3(lum, lum, lum), albedo, _Saturation);
                albedo = saturate((albedo - 0.5h) * _Contrast + 0.5h) * _Tint.rgb;

                // Damp ground at the water's edge. The tile art already carries
                // the grass-to-dirt run - GroundSurface picks dirtier slices as
                // it approaches the water - so this only has to darken, not
                // recolour.
                albedo *= 1.0h - shore * _ShoreDamp * isTop;

                // Without this the tiles read as one printed sheet: the facets
                // the mesh is built from stop being visible at all.
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
