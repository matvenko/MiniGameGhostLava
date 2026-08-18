// Ground shader that samples by WORLD POSITION instead of per-mesh UVs, so a
// field of 1x1 cubes reads as one continuous surface rather than restarting the
// texture on every cube. Same trick the AQUIS water uses for the liquid pools.
//
// Triplanar rather than a flat top-down projection: the tiles are cubes and
// their side faces are visible from this camera, and a single XZ projection
// would smear those sides into vertical streaks.
Shader "Custom/TriplanarGround_URP"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        [HDR] _BaseColor ("Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1
        _Smoothness ("Smoothness", Range(0,1)) = 0.15
        _MapScale ("World Units Per Texture Tile", Float) = 4
        _BlendSharpness ("Triplanar Blend Sharpness", Range(1,16)) = 6
        _AmbientColor ("Ambient", Color) = (0.30, 0.32, 0.36, 1)

        [Header(Colour Grading)]
        _ShadowTint ("Shadow Tint", Color) = (0.10, 0.28, 0.26, 1)
        _HighlightTint ("Highlight Tint", Color) = (0.55, 0.78, 0.42, 1)
        _TintBlend ("Tint Blend", Range(0,1)) = 0.6
        _Saturation ("Saturation", Range(0,2)) = 1.1
        _Contrast ("Contrast", Range(0.5,2)) = 1.0
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

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float  _NormalStrength;
            float  _Smoothness;
            float  _MapScale;
            float  _BlendSharpness;
            float4 _AmbientColor;
            float4 _ShadowTint;
            float4 _HighlightTint;
            float  _TintBlend;
            float  _Saturation;
            float  _Contrast;
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

            // Deliberately no _MAIN_LIGHT_SHADOWS_SCREEN variant: that path expects
            // a screen-space shadow coord, and feeding it a world-derived one made
            // the whole surface resolve to zero attenuation (fully shadowed/black).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
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
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // Weights for the three planar projections. Raising to a power keeps
            // the seams where two projections meet narrow instead of muddy.
            float3 TriplanarWeights(float3 n)
            {
                float3 w = pow(abs(n), _BlendSharpness);
                return w / max(dot(w, 1.0), 1e-4);
            }

            half3 TriplanarAlbedo(float3 uvw, float3 w)
            {
                half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvw.zy).rgb;
                half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvw.xz).rgb;
                half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvw.xy).rgb;
                return x * w.x + y * w.y + z * w.z;
            }

            // Re-palettes the photographic texture without losing its detail: keep
            // the luminance, but push the hue toward a two-colour ramp. That is
            // what lets a stock grass photo sit next to the stylised water instead
            // of fighting it - a plain multiply tint can only darken, never shift hue.
            half3 Grade(half3 c)
            {
                half lum = saturate(Luminance(c));
                lum = saturate((lum - 0.5h) * _Contrast + 0.5h);

                half3 ramp = lerp(_ShadowTint.rgb, _HighlightTint.rgb, lum);
                half3 graded = lerp(c, ramp, _TintBlend);

                return max(0.0h, lerp(Luminance(graded).xxx, graded, _Saturation));
            }

            // Whiteout blend: reorients each tangent-space sample onto the
            // geometric normal, which avoids the axis-flip artefacts you get
            // from naively lerping the three normal samples together.
            half3 TriplanarNormal(float3 uvw, float3 w, float3 n)
            {
                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvw.zy), _NormalStrength);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvw.xz), _NormalStrength);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvw.xy), _NormalStrength);

                tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);

                return normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 n = normalize(IN.normalWS);
                float3 uvw = IN.positionWS / max(_MapScale, 1e-4);
                float3 w = TriplanarWeights(n);

                half3 albedo = Grade(TriplanarAlbedo(uvw, w)) * _BaseColor.rgb;
                half3 N = TriplanarNormal(uvw, w, n);

                // Explicit main light + SH ambient rather than UniversalFragmentPBR:
                // that path wants URP 17's probe/lightmap plumbing (OUTPUT_SH4 and
                // friends) and silently resolves to black without it. A ground plane
                // needs no metallic BRDF anyway, and this is cheaper on mobile.
                // Only sample the shadow map when one is actually being rendered.
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                #else
                    Light mainLight = GetMainLight();
                #endif
                // distanceAttenuation comes from unity_LightData (UnityPerDraw),
                // which is not reliably bound for this pass and drags the surface
                // dark. It is 1 for a directional light by definition anyway, so
                // only the shadow term is worth applying - and only when a shadow
                // map is actually being rendered.
                half atten = 1.0h;
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    atten = mainLight.shadowAttenuation;
                #endif
                half ndotl = saturate(dot(N, mainLight.direction));

                // Flat ambient rather than SampleSH: the SH coefficients are not
                // reliably bound for a hand-written pass here and came back NaN,
                // which poisoned the whole sum and rendered the ground black.
                // A constant fill is also predictable to art-direct and cheaper.
                half3 ambient = _AmbientColor.rgb;
                half3 color = albedo * (mainLight.color * ndotl * atten + ambient);

                // Broad, cheap sheen so the normal map still reads under the
                // top-down camera instead of flattening out.
                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half3 halfVec = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(N, halfVec)), lerp(4.0, 64.0, _Smoothness));
                color += mainLight.color * spec * _Smoothness * atten;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Without these the tiles would drop out of the shadow map and out of the
        // camera depth texture - and the water's depth fade reads that depth
        // texture, so its shoreline would break.
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
