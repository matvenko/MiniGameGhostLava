// Flat-shaded, untextured ground for the low-poly look: colour comes from a
// two-stop ramp driven by a per-facet variation value baked into the vertex
// colours, so the surface reads as faceted terrain rather than a tiled photo.
//
// Vertex colour layout (written by GroundSurface.cs):
//   rgb = facet variation 0..1, remapped between the dark and light stops
//   a   = 1 on top faces, 0 on the side walls that drop into the water
Shader "Custom/LowPolyGround_URP"
{
    Properties
    {
        _ColorDark ("Grass Dark", Color) = (0.13, 0.42, 0.24, 1)
        _ColorLight ("Grass Light", Color) = (0.42, 0.78, 0.38, 1)
        _SideDark ("Side Dark", Color) = (0.10, 0.22, 0.20, 1)
        _SideLight ("Side Light", Color) = (0.20, 0.36, 0.30, 1)
        _AmbientColor ("Ambient", Color) = (0.26, 0.34, 0.38, 1)
        _RimColor ("Rim", Color) = (0.55, 0.90, 0.70, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.15
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _ColorDark;
            float4 _ColorLight;
            float4 _SideDark;
            float4 _SideLight;
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

                half variation = IN.color.r;
                half isTop = IN.color.a;
                half3 grass = lerp(_ColorDark.rgb, _ColorLight.rgb, variation);
                half3 side  = lerp(_SideDark.rgb, _SideLight.rgb, variation);
                half3 albedo = lerp(side, grass, isTop);

                // Same explicit lighting as the triplanar ground: URP 17's probe
                // and per-draw plumbing is not reliable for hand-written passes.
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                    half atten = mainLight.shadowAttenuation;
                #else
                    Light mainLight = GetMainLight();
                    half atten = 1.0h;
                #endif

                half ndotl = saturate(dot(N, mainLight.direction));
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
