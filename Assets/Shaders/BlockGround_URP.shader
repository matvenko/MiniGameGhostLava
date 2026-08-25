// The board tile: grass on the lid, earth down the sides.
//
// Two textures, chosen by the face normal, because the art comes that way - a
// square of meadow seen from above, and a cut-away bank whose grass fringe sits
// along the TOP edge of the image. Neither fits an atlas slot on the pack cube
// without repainting the art, and neither wants per-mesh UVs anyway.
//
// So both are projected from world position, exactly like TriplanarGround_URP:
// the grass runs continuously across neighbouring tiles instead of restarting on
// every cube, and so does the earth along a shoreline. The one coordinate that
// is NOT world-derived is the side V - that comes from object space, so the
// fringe lands on the top edge of the cube wherever the tile sits and however
// deep the board is sunk.
Shader "Custom/BlockGround_URP"
{
    Properties
    {
        _TopMap ("Top (grass)", 2D) = "white" {}
        _SideMap ("Side (earth)", 2D) = "white" {}
        [HDR] _BaseColor ("Tint", Color) = (1,1,1,1)

        _TopScale ("World Units Per Grass Tile", Float) = 4
        _SideScale ("World Units Per Earth Tile", Float) = 4
        _SideStretch ("Earth Heights Per Cube", Range(0.1,12)) = 1
        _SideOffset ("Earth Vertical Offset", Range(-1,1)) = 0
        _SideBrightness ("Earth Brightness", Range(0.5,3)) = 1
        _BlendSharpness ("Top/Side Blend Sharpness", Range(1,16)) = 8

        [Header(Tile Seam)]
        _SeamStrength ("Seam Darkening", Range(0,1)) = 0.12
        _SeamWidth ("Seam Width", Range(0.005,0.2)) = 0.045
        _CellVariation ("Per Cell Brightness Variation", Range(0,0.5)) = 0

        [Header(Lighting)]
        _AmbientColor ("Ambient From Above", Color) = (0.34, 0.36, 0.40, 1)
        _AmbientGround ("Ambient From Below", Color) = (0.30, 0.29, 0.26, 1)
        _LightWrap ("Light Wrap", Range(0,1)) = 0.6
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        // One shared block, so the UnityPerMaterial layout is identical in every
        // pass - SRP batching drops the shader otherwise.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TopMap_ST;
            float4 _SideMap_ST;
            float4 _BaseColor;
            float  _TopScale;
            float  _SideScale;
            float  _SideStretch;
            float  _SideOffset;
            float  _SideBrightness;
            float  _BlendSharpness;
            float  _SeamStrength;
            float  _SeamWidth;
            float  _CellVariation;
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

            // No _MAIN_LIGHT_SHADOWS_SCREEN variant, for the same reason as the
            // triplanar ground: that path wants a screen-space shadow coord and
            // resolves a world-derived one to full shadow.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TopMap);   SAMPLER(sampler_TopMap);
            TEXTURE2D(_SideMap);  SAMPLER(sampler_SideMap);

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
                float  heightOS    : TEXCOORD2;
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
                // The pack cube is a unit mesh centred on its origin, so this is
                // 0 at the foot of the tile and 1 at its lid.
                OUT.heightOS = IN.positionOS.y + 0.5;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // A hairline of shade where one cell meets the next. Without it a
            // world-projected grass field loses the grid entirely and the board
            // stops reading as tiles.
            half Seam(float2 xz)
            {
                float2 f = abs(frac(xz + 0.5) - 0.5);
                float d = min(f.x, f.y);
                return 1.0h - _SeamStrength * (1.0h - smoothstep(0.0, _SeamWidth, d));
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 n = normalize(IN.normalWS);

                // Weights over the three planes, as triplanar - but the Y plane
                // reads the grass and the two side planes read the earth.
                float3 w = pow(abs(n), _BlendSharpness);
                w /= max(dot(w, 1.0), 1e-4);

                float2 uvTop = IN.positionWS.xz / max(_TopScale, 1e-4);
                float  vSide = (IN.heightOS - 1.0) * _SideStretch + 1.0 + _SideOffset;
                float2 uvSideX = float2(IN.positionWS.z / max(_SideScale, 1e-4), vSide);
                float2 uvSideZ = float2(IN.positionWS.x / max(_SideScale, 1e-4), vSide);

                half3 top   = SAMPLE_TEXTURE2D(_TopMap,  sampler_TopMap,  uvTop).rgb;
                // The earth art is painted much darker than the grass, so no
                // amount of fill light alone brings the banks up to it. The lift
                // belongs on the side albedo, where it cannot wash out the lawn.
                half3 sideX = SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, uvSideX).rgb * _SideBrightness;
                half3 sideZ = SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, uvSideZ).rgb * _SideBrightness;

                half3 albedo = (sideX * w.x + top * w.y + sideZ * w.z) * _BaseColor.rgb;
                albedo *= lerp(1.0h, Seam(IN.positionWS.xz), w.y);

                // One brightness per grid cell. A wall of 108 stones off the same
                // mesh and the same material is otherwise 108 identical stones,
                // however much their scale and yaw are shuffled - and the eye
                // reads the repeat long before it reads the silhouette.
                float2 cell = floor(IN.positionWS.xz + 0.5);
                float h = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                albedo *= 1.0h + (h - 0.5h) * 2.0h * _CellVariation;

                // Explicit main light plus a flat ambient, matching the other
                // hand-written surfaces in this project: UniversalFragmentPBR
                // wants the URP 17 probe plumbing and resolves to black without it.
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                    half atten = mainLight.shadowAttenuation;
                #else
                    Light mainLight = GetMainLight();
                    half atten = 1.0h;
                #endif

                // Wrapped diffuse: the term never reaches zero, so the face
                // turned away from the sun keeps its colour instead of going to
                // black. A board seen from directly above has four side faces at
                // every pool, and two of them always point away from the light.
                half ndotl = saturate((dot(n, mainLight.direction) + _LightWrap) / (1.0h + _LightWrap));

                // Hemispheric fill - sky above, warm bounce off the ground below -
                // so the light arrives from every direction and not one.
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

        // Tiles have to stay in the shadow map and the depth texture: the water
        // reads camera depth for its shoreline fade.
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
