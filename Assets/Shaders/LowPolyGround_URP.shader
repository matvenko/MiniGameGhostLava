// Textured ground for the merged floor mesh. The faceted low-poly silhouette
// stays, but the surface is no longer a flat two-stop green ramp: it samples
// real grass, flower-meadow and soil photos by WORLD POSITION, so the whole
// board reads as one continuous field instead of a painted plane.
//
// Three layers, mixed per fragment:
//   grass   - _BaseMap, the base meadow
//   flowers - _FlowerMap, blended in as soft patches driven by world noise
//   soil    - _SoilMap, on the skirt walls and fading in along the shoreline
//
// Vertex colour layout (written by GroundSurface.cs):
//   r = facet variation 0..1, used as a subtle per-facet brightness tint
//   g = shoreline proximity 0..1, 1 right at the water's edge
//   a = 1 on top faces, 0 on the side walls that drop into the water
//
// Tiling repetition is the usual failure mode for world-projected textures at
// this camera distance, so a low-frequency noise multiplies brightness on top
// of everything to break the grid up.
Shader "Custom/LowPolyGround_URP"
{
    Properties
    {
        [Header(Grass)]
        _BaseMap ("Grass Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Grass Normal", 2D) = "bump" {}
        _GrassTint ("Grass Tint", Color) = (0.82, 1.0, 0.72, 1)
        _GrassScale ("Grass World Tile Size", Float) = 3

        [Header(Flower Patches)]
        _FlowerMap ("Flower Albedo", 2D) = "white" {}
        [Normal] _FlowerNormal ("Flower Normal", 2D) = "bump" {}
        _FlowerTint ("Flower Tint", Color) = (0.95, 1.0, 0.85, 1)
        _FlowerScale ("Flower World Tile Size", Float) = 4
        _PatchScale ("Patch Size (world units)", Float) = 11
        _PatchAmount ("Patch Coverage", Range(0,1)) = 0.45
        _PatchSoftness ("Patch Softness", Range(0.01,0.6)) = 0.22

        [Header(Soil)]
        _SoilMap ("Soil Albedo", 2D) = "white" {}
        [Normal] _SoilNormal ("Soil Normal", 2D) = "bump" {}
        _SoilTint ("Soil Tint", Color) = (1, 0.95, 0.85, 1)
        _SoilScale ("Soil World Tile Size", Float) = 2.5
        _ShoreWidth ("Shoreline Dirt Width", Range(0,1)) = 0.55
        _ShoreDamp ("Shoreline Darkening", Range(0,1)) = 0.35

        [Header(Stylisation)]
        _Stylize ("Push Towards Palette", Range(0,1)) = 0.5
        _StyleGrassDark ("Style Grass Dark", Color) = (0.13, 0.42, 0.24, 1)
        _StyleGrassLight ("Style Grass Light", Color) = (0.46, 0.80, 0.40, 1)
        _StyleSoilDark ("Style Soil Dark", Color) = (0.26, 0.19, 0.14, 1)
        _StyleSoilLight ("Style Soil Light", Color) = (0.60, 0.47, 0.34, 1)

        [Header(Surface Detail)]
        _NormalStrength ("Normal Strength", Range(0,2)) = 0.9
        _MacroScale ("Macro Variation Size", Float) = 17
        _MacroStrength ("Macro Variation", Range(0,1)) = 0.3
        _FacetStrength ("Facet Variation", Range(0,1)) = 0.18
        _Saturation ("Saturation", Range(0,2)) = 1.15

        [Header(Lighting)]
        _AmbientColor ("Ambient", Color) = (0.26, 0.32, 0.36, 1)
        _RimColor ("Rim", Color) = (0.55, 0.90, 0.70, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.14
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        // Shared by every pass so the UnityPerMaterial layout stays identical -
        // SRP batching silently drops the shader otherwise. SurfaceInput.hlsl
        // supplies _BaseMap/_BumpMap and the helpers the stock URP shadow and
        // depth passes expect to find already declared.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        TEXTURE2D(_FlowerMap);   SAMPLER(sampler_FlowerMap);
        TEXTURE2D(_FlowerNormal);
        TEXTURE2D(_SoilMap);     SAMPLER(sampler_SoilMap);
        TEXTURE2D(_SoilNormal);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _GrassTint;
            float  _GrassScale;
            float4 _FlowerTint;
            float  _FlowerScale;
            float  _PatchScale;
            float  _PatchAmount;
            float  _PatchSoftness;
            float4 _SoilTint;
            float  _SoilScale;
            float  _ShoreWidth;
            float  _ShoreDamp;
            float  _Stylize;
            float4 _StyleGrassDark;
            float4 _StyleGrassLight;
            float4 _StyleSoilDark;
            float4 _StyleSoilLight;
            float  _NormalStrength;
            float  _MacroScale;
            float  _MacroStrength;
            float  _FacetStrength;
            float  _Saturation;
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

            float GroundHash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float GroundValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = GroundHash21(i);
                float b = GroundHash21(i + float2(1, 0));
                float c = GroundHash21(i + float2(0, 1));
                float d = GroundHash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float GroundFbm(float2 p)
            {
                return GroundValueNoise(p) * 0.6 + GroundValueNoise(p * 2.3) * 0.3 + GroundValueNoise(p * 5.1) * 0.1;
            }

            // Remaps a photographic layer onto a two-stop palette ramp, keeping
            // its luminance detail but throwing away its real hues. This is what
            // keeps the ground in the same visual language as the stylised water:
            // full detail, but painted colours rather than photographed ones.
            half3 GroundStylize(half3 c, half3 dark, half3 light, half amount)
            {
                half lum = dot(c, half3(0.299h, 0.587h, 0.114h));
                half t = saturate((lum - 0.12h) / 0.5h);
                return lerp(c, lerp(dark, light, t), amount);
            }

            // Perturb a flat-shaded normal with a tangent-space map, using the
            // projection axes the albedo was sampled with as the tangent frame.
            float3 GroundPerturbNormal(float3 n, float3 tn, float3 t, float3 b, float strength)
            {
                return normalize(n + (t * tn.x + b * tn.y) * strength);
            }

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

                half isTop = IN.color.a;
                half variation = IN.color.r;
                half shore = IN.color.g;

                // Top faces project straight down; the skirt walls project along
                // whichever horizontal axis they face, so the soil does not smear
                // into vertical streaks.
                bool useZ = abs(N.x) > abs(N.z);
                float2 uvSide = useZ ? float2(pw.z, pw.y) : float2(pw.x, pw.y);
                float3 sideT = useZ ? float3(0, 0, 1) : float3(1, 0, 0);

                float2 uvTop = pw.xz;

                // --- top layers -------------------------------------------------
                float2 grassUV = uvTop / max(_GrassScale, 0.01);
                float2 flowerUV = uvTop / max(_FlowerScale, 0.01);

                half3 grass = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, grassUV).rgb * _GrassTint.rgb;
                half3 flowers = SAMPLE_TEXTURE2D(_FlowerMap, sampler_FlowerMap, flowerUV).rgb * _FlowerTint.rgb;

                float3 grassN = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, grassUV));
                float3 flowerN = UnpackNormal(SAMPLE_TEXTURE2D(_FlowerNormal, sampler_FlowerMap, flowerUV));

                // Soft, irregular meadow patches rather than a hard mask.
                float patchNoise = GroundFbm(uvTop / max(_PatchScale, 0.01));
                float threshold = lerp(0.85, 0.25, saturate(_PatchAmount));
                half patch = smoothstep(threshold - _PatchSoftness, threshold + _PatchSoftness, patchNoise);

                half3 topAlbedo = lerp(grass, flowers, patch);
                float3 topNormalTS = lerp(grassN, flowerN, patch);
                topAlbedo = GroundStylize(topAlbedo, _StyleGrassDark.rgb, _StyleGrassLight.rgb, _Stylize);

                // --- soil, on the walls and creeping up the shoreline -----------
                float2 soilUVTop = uvTop / max(_SoilScale, 0.01);
                float2 soilUVSide = uvSide / max(_SoilScale, 0.01);
                float2 soilUV = isTop > 0.5h ? soilUVTop : soilUVSide;

                half3 soil = SAMPLE_TEXTURE2D(_SoilMap, sampler_SoilMap, soilUV).rgb * _SoilTint.rgb;
                float3 soilN = UnpackNormal(SAMPLE_TEXTURE2D(_SoilNormal, sampler_SoilMap, soilUV));
                soil = GroundStylize(soil, _StyleSoilDark.rgb, _StyleSoilLight.rgb, _Stylize);

                // The shoreline band is noisy so the grass/dirt boundary wanders
                // instead of tracing the cell grid.
                float shoreEdge = saturate(shore * (0.75 + GroundFbm(uvTop * 0.7) * 0.6));
                half shoreMask = smoothstep(1.0h - _ShoreWidth, 1.0h, shoreEdge);

                // Walls are pure soil; only the top blends grass into it.
                half3 albedo = lerp(soil, lerp(topAlbedo, soil, shoreMask), isTop);
                float3 normalTS = lerp(soilN, lerp(topNormalTS, soilN, shoreMask), isTop);

                float3 T = isTop > 0.5h ? float3(1, 0, 0) : sideT;
                float3 B = isTop > 0.5h ? float3(0, 0, 1) : float3(0, 1, 0);

                // Damp the ground right where it meets the water, so the bank
                // reads as damp earth instead of the same flat green everywhere.
                albedo *= lerp(1.0h, 1.0h - _ShoreDamp, shoreMask * isTop);

                // --- variation --------------------------------------------------
                float macro = GroundFbm(uvTop / max(_MacroScale, 0.01));
                albedo *= lerp(1.0h, 0.7h + macro * 0.7h, _MacroStrength);
                albedo *= 1.0h + (variation - 0.5h) * _FacetStrength;

                half lum = dot(albedo, half3(0.299h, 0.587h, 0.114h));
                albedo = lerp(half3(lum, lum, lum), albedo, _Saturation);

                float3 n = GroundPerturbNormal(N, normalTS, T, B, _NormalStrength);

                // Same explicit lighting as the triplanar ground: URP 17's probe
                // and per-draw plumbing is not reliable for hand-written passes.
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                    half atten = mainLight.shadowAttenuation;
                #else
                    Light mainLight = GetMainLight();
                    half atten = 1.0h;
                #endif

                half ndotl = saturate(dot(n, mainLight.direction));
                // Half-lambert keeps the shaded facets readable instead of black,
                // which is what the stylised look wants.
                half wrapped = ndotl * 0.5h + 0.5h;
                wrapped *= wrapped;

                half3 color = albedo * (mainLight.color * wrapped * atten + _AmbientColor.rgb);

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
