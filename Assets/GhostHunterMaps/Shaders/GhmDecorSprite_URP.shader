// Painted decoration standing on the board: flowers, tufts, stones, whatever
// the catalogue holds.
//
// The quads arrive already rotated to face the rig, so there is no billboarding
// here - the camera angle is an authored setting, not something that moves, and
// baking it into the mesh is what makes the published board identical to the
// preview. Alpha is clipped rather than blended so hundreds of sprites can
// overlap in any order without sorting artefacts.
Shader "GhostHunterMaps/DecorSprite"
{
    Properties
    {
        [MainTexture] _BaseMap("Sprite", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha cutoff", Range(0, 1)) = 0.4
        _AmbientBoost("Ambient boost", Range(0, 2)) = 0.85
        _LightWrap("Light wrap", Range(0, 1)) = 0.6
        _GroundShade("Base darkening", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Quads are single-sided geometry seen from both sides once the rig
            // yaws, so backface culling would make half of them vanish.
            Cull Off
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 color      : COLOR;
                float  fogFactor  : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _AmbientBoost;
                float _LightWrap;
                float _GroundShade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = tex * _BaseColor * input.color;
                clip(albedo.a - _Cutoff);

                float3 normal = normalize(input.normalWS);
                // Painted art has its own shading baked in; flipping the normal
                // towards the viewer keeps the lighting from fighting it and
                // turning the back half of every sprite black.
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                normal = dot(normal, viewDir) < 0 ? -normal : normal;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Wrapped lambert: leaves are thin, so light bleeds around them
                // rather than terminating at the silhouette.
                half ndotl = dot(normal, mainLight.direction);
                half wrapped = saturate((ndotl + _LightWrap) / (1.0 + _LightWrap));
                half3 lit = albedo.rgb * mainLight.color * wrapped * mainLight.shadowAttenuation;
                lit += albedo.rgb * SampleSH(normal) * _AmbientBoost;

                // A touch of contact shading at the foot of the sprite so it sits
                // on the ground instead of hovering over it.
                lit *= lerp(1.0 - _GroundShade, 1.0, saturate(input.uv.y * 1.6));

                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _AmbientBoost;
                float _LightWrap;
                float _GroundShade;
            CBUFFER_END

            float3 _LightDirection;

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
