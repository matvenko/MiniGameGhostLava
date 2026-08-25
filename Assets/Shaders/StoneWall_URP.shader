// The rampart, built from painted stone modules instead of tinted facets.
//
// The art arrives as a set with a fixed aspect: one square cap seen from above,
// and four 1:2 elevations of the same wall seen from the side. So the geometry
// is cut to match it - one module is a 1x1 footprint standing two units tall -
// and the mesh carries real UVs, one full copy of a texture per face. Nothing is
// projected and nothing tiles across a face, which is what lets the painted
// mortar joints land exactly on the module edges.
//
// Which of the four elevations a face gets is decided in WallSurface.cs and
// arrives in uv2.x; uv2.y is a per-module brightness so 54 modules off one
// material do not read as 54 copies. Sampling is branched rather than blended,
// with explicit gradients, so a side face costs one fetch and the mip chain
// still resolves correctly across the branch.
//
// Lighting is BlockGround_URP's, deliberately: hemispheric fill plus wrapped
// diffuse. The wall and the floor have to answer to light the same way or the
// board falls apart into separate props again.
Shader "Custom/StoneWall_URP"
{
    Properties
    {
        _TopMap ("Cap - square 1:1", 2D) = "white" {}
        _SideMap0 ("Side A - 1:2", 2D) = "white" {}
        _SideMap1 ("Side B - 1:2", 2D) = "white" {}
        _SideMap2 ("Side C - 1:2", 2D) = "white" {}
        _SideMap3 ("Side D - 1:2", 2D) = "white" {}

        [HDR] _BaseColor ("Tint", Color) = (1,1,1,1)
        _CapTint ("Cap Tint", Color) = (1,1,1,1)
        _ModuleVariation ("Per Module Brightness", Range(0,0.5)) = 0.1

        [Header(Grounding)]
        _FootShade ("Foot Shading", Range(0,1)) = 0.35
        _FootHeight ("Foot Height (uv)", Range(0,1)) = 0.25

        [Header(Lighting)]
        _AmbientColor ("Ambient From Above", Color) = (0.34, 0.36, 0.40, 1)
        _AmbientGround ("Ambient From Below", Color) = (0.30, 0.29, 0.26, 1)
        _LightWrap ("Light Wrap", Range(0,1)) = 0.7
        _Smoothness ("Smoothness", Range(0,1)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        // One shared block so the UnityPerMaterial layout is identical in every
        // pass - SRP batching drops the shader otherwise.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TopMap_ST;
            float4 _SideMap0_ST;
            float4 _SideMap1_ST;
            float4 _SideMap2_ST;
            float4 _SideMap3_ST;
            float4 _BaseColor;
            float4 _CapTint;
            float  _ModuleVariation;
            float  _FootShade;
            float  _FootHeight;
            float4 _AmbientColor;
            float4 _AmbientGround;
            float  _LightWrap;
            float  _Smoothness;
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

            // No _MAIN_LIGHT_SHADOWS_SCREEN variant, matching the other surfaces
            // here: that path wants a screen-space shadow coord and resolves a
            // world-derived one to full shadow.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TopMap);    SAMPLER(sampler_TopMap);
            TEXTURE2D(_SideMap0);  SAMPLER(sampler_SideMap0);
            TEXTURE2D(_SideMap1);  SAMPLER(sampler_SideMap1);
            TEXTURE2D(_SideMap2);  SAMPLER(sampler_SideMap2);
            TEXTURE2D(_SideMap3);  SAMPLER(sampler_SideMap3);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 module     : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float2 module      : TEXCOORD3;
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
                OUT.module = IN.module;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // The variant index is constant across a face, so the branch is
            // uniform over every quad and only ever diverges on the pixels of a
            // module edge. Gradients are taken before the branch: read inside
            // one, a plain sample would have no defined mip level.
            half3 SampleSide(float variant, float2 uv, float2 dx, float2 dy)
            {
                if (variant < 0.5) return SAMPLE_TEXTURE2D_GRAD(_SideMap0, sampler_SideMap0, uv, dx, dy).rgb;
                if (variant < 1.5) return SAMPLE_TEXTURE2D_GRAD(_SideMap1, sampler_SideMap1, uv, dx, dy).rgb;
                if (variant < 2.5) return SAMPLE_TEXTURE2D_GRAD(_SideMap2, sampler_SideMap2, uv, dx, dy).rgb;
                return SAMPLE_TEXTURE2D_GRAD(_SideMap3, sampler_SideMap3, uv, dx, dy).rgb;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 n = normalize(IN.normalWS);
                float2 uv = IN.uv;
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);

                // Every face is axis aligned, so the cap is simply the one whose
                // normal points up - no extra vertex channel needed to flag it.
                half3 albedo;
                if (n.y > 0.5)
                {
                    albedo = SAMPLE_TEXTURE2D_GRAD(_TopMap, sampler_TopMap, uv, dx, dy).rgb * _CapTint.rgb;
                }
                else
                {
                    albedo = SampleSide(IN.module.x, uv, dx, dy);
                    // The foot of the wall is where it meets the board, and a
                    // painted elevation has no contact shadow of its own.
                    albedo *= lerp(1.0h - _FootShade, 1.0h, smoothstep(0.0, max(_FootHeight, 1e-4), uv.y));
                }

                albedo *= _BaseColor.rgb;
                albedo *= 1.0h + (IN.module.y - 0.5h) * 2.0h * _ModuleVariation;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                    half atten = mainLight.shadowAttenuation;
                #else
                    Light mainLight = GetMainLight();
                    half atten = 1.0h;
                #endif

                // Wrapped diffuse: a ring of wall always has two sides turned
                // away from the sun, and they have to keep their painted colour
                // rather than fall to black.
                half ndotl = saturate((dot(n, mainLight.direction) + _LightWrap) / (1.0h + _LightWrap));
                half3 ambient = lerp(_AmbientGround.rgb, _AmbientColor.rgb, saturate(n.y * 0.5h + 0.5h));

                half3 color = albedo * (mainLight.color * ndotl * atten + ambient);

                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half3 halfVec = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(n, halfVec)), lerp(4.0, 64.0, _Smoothness));
                color += mainLight.color * spec * _Smoothness * atten;

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
