// The pool surface: still, desaturated teal water with soft caustic cells,
// drawn procedurally rather than from a photo of a swimming pool.
//
// The reference this is built to is a calm sea seen from directly overhead -
// rounded blobs of slightly different value packed against each other, pale
// creases where they meet, and a scattering of thin bright ripples. That is a
// Worley cell field almost exactly: the cell id gives each blob its own value,
// and the gap between the nearest and second-nearest feature point gives the
// crease. Two of those fields at different scales, drifting against each other,
// are what make it read as moving water instead of a sliding texture.
//
// It is sampled from world XZ, not from mesh UVs, so the pattern runs
// continuously across the whole board and every pool on it belongs to the same
// body of water - which is the point of LiquidSurface merging them into one
// mesh in the first place.
//
// Opaque, because the reference is: this water has no bottom to show. The pool
// bed underneath is left in place but never seen.
//
// The feature points are drifted by scrolling the sample position and warping
// it slightly, not by animating each point with a sin - the second costs a
// transcendental per cell per octave, and at this camera nobody can tell them
// apart.
Shader "Custom/StylizedWater_URP"
{
    Properties
    {
        [Header(Palette)]
        _DeepColor ("Deep", Color) = (0.216, 0.412, 0.404, 1)
        _LightColor ("Light", Color) = (0.373, 0.573, 0.529, 1)
        _CreaseColor ("Crease", Color) = (0.478, 0.667, 0.612, 1)
        _SparkleColor ("Sparkle", Color) = (0.82, 0.89, 0.83, 1)

        [Header(Cells)]
        _CellScale ("Cells Per World Unit", Float) = 4.5
        _CellContrast ("Blob Value Spread", Range(0,1)) = 0.85
        _CellRound ("Blob Falloff", Range(0.5,4)) = 1.7
        _DetailScale ("Detail Scale Multiplier", Range(1,5)) = 2.3
        _DetailWeight ("Detail Weight", Range(0,1)) = 0.42
        _BroadScale ("Broad Drift Scale", Range(0.05,1)) = 0.3
        _BroadWeight ("Broad Drift Weight", Range(0,1)) = 0.28

        [Header(Creases and Sparkle)]
        _CreaseWidth ("Crease Width", Range(0.01,0.6)) = 0.45
        _CreaseStrength ("Crease Strength", Range(0,1)) = 0.3
        _SparklePower ("Sparkle Tightness", Range(1,24)) = 14
        _SparkleStrength ("Sparkle Strength", Range(0,1)) = 0.22
        _SparkleCoverage ("Sparkle Coverage", Range(0,1)) = 0.35

        [Header(Motion)]
        _Speed ("Drift Speed", Range(0,1)) = 0.06
        _Warp ("Warp Amount", Range(0,0.5)) = 0.09
        _WarpSpeed ("Warp Speed", Range(0,3)) = 0.5

        [Header(Lighting)]
        _AmbientColor ("Ambient", Color) = (0.55, 0.58, 0.60, 1)
        _SunStrength ("Sun Strength", Range(0,2)) = 0.55
        _LightWrap ("Light Wrap", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        // One shared block, so the UnityPerMaterial layout is identical in every
        // pass - SRP batching drops the shader otherwise.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _DeepColor;
            float4 _LightColor;
            float4 _CreaseColor;
            float4 _SparkleColor;
            float  _CellScale;
            float  _CellContrast;
            float  _CellRound;
            float  _DetailScale;
            float  _DetailWeight;
            float  _BroadScale;
            float  _BroadWeight;
            float  _CreaseWidth;
            float  _CreaseStrength;
            float  _SparklePower;
            float  _SparkleStrength;
            float  _SparkleCoverage;
            float  _Speed;
            float  _Warp;
            float  _WarpSpeed;
            float4 _AmbientColor;
            float  _SunStrength;
            float  _LightWrap;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float  fogFactor   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float Hash12(float2 p)
            {
                return frac(sin(dot(p, float2(41.7, 289.3))) * 24634.6345);
            }

            // Worley over the 3x3 neighbourhood.
            //   x = distance to the nearest feature point
            //   y = distance to the second nearest
            //   z = a 0..1 value belonging to the nearest cell
            // The gap between x and y is the only reliable way to find a cell
            // wall: the distance itself has no idea where the boundary is.
            float3 Cells(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = p - ip;

                float f1 = 8.0, f2 = 8.0, id = 0.0;
                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 cell = ip + g;
                        float2 r = g + Hash22(cell) - fp;
                        float d = dot(r, r);
                        if (d < f1)
                        {
                            f2 = f1;
                            f1 = d;
                            id = Hash12(cell);
                        }
                        else if (d < f2)
                        {
                            f2 = d;
                        }
                    }
                }
                return float3(sqrt(f1), sqrt(f2), id);
            }

            // Plain smoothed value noise. This is here for the broad drift only,
            // and four hashes are a quarter of what another Worley octave would
            // have cost for something that is never seen as structure anyway.
            float ValueNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = p - ip;
                fp = fp * fp * (3.0 - 2.0 * fp);

                float a = Hash12(ip);
                float b = Hash12(ip + float2(1, 0));
                float c = Hash12(ip + float2(0, 1));
                float d = Hash12(ip + float2(1, 1));
                return lerp(lerp(a, b, fp.x), lerp(c, d, fp.x), fp.y);
            }

            // One octave of the surface: a drifting, slightly warped cell field.
            // Returns the blob value in x and the crease mask in y.
            float2 Layer(float2 xz, float scale, float2 drift, float t)
            {
                float2 p = xz * scale + drift * t * _Speed * scale;
                // A little domain warp, so the field breathes instead of sliding
                // past as a rigid sheet.
                p += _Warp * float2(sin(p.y * 1.7 + t * _WarpSpeed),
                                    cos(p.x * 1.5 - t * _WarpSpeed * 0.8));

                float3 c = Cells(p);
                float crease = 1.0 - smoothstep(0.0, _CreaseWidth, c.y - c.x);

                // Each blob carries its own value at its centre and fades back to
                // neutral on the way out, so neighbours always meet at the same
                // value. Handing every cell one flat tone instead gives a faceted
                // mosaic with a hard line at every wall - which is what a pool
                // seen from above is not.
                float radial = saturate(c.x * _CellRound);
                float tone = lerp(c.z, 0.5, radial * radial);

                return float2(tone, crease);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float t = _Time.y;
                float2 xz = IN.positionWS.xz;

                float2 a = Layer(xz, _CellScale, float2(1.0, 0.35), t);
                float2 b = Layer(xz, _CellScale * _DetailScale, float2(-0.6, 0.9), t);

                // The two fields interfere rather than stack: the small one
                // nudges the big one's value, which is what stops the blobs from
                // reading as a single regular lattice.
                float tone = lerp(a.x, b.x, _DetailWeight);
                // A slow swell far larger than any blob, so the pool has open
                // paler stretches and darker ones instead of an even speckle
                // across its whole area.
                float broad = ValueNoise(xz * _BroadScale + float2(0.13, -0.09) * t * _Speed);
                tone = lerp(tone, broad, _BroadWeight);
                tone = saturate(0.5 + (tone - 0.5) * _CellContrast);

                float crease = saturate(a.y + b.y * _DetailWeight);

                half3 col = lerp(_DeepColor.rgb, _LightColor.rgb, tone);
                col = lerp(col, _CreaseColor.rgb, crease * _CreaseStrength);

                // Only some creases catch the light, otherwise every wall in the
                // field glows and the surface turns into a net. The detail
                // layer's own blob value is the mask - it is already a smooth
                // field at the right scale and costs nothing more.
                float coverage = smoothstep(0.85 - _SparkleCoverage, 1.0 - _SparkleCoverage * 0.5, b.x);
                float sparkle = pow(crease, _SparklePower) * coverage;
                col += _SparkleColor.rgb * sparkle * _SparkleStrength;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                    half atten = mainLight.shadowAttenuation;
                #else
                    Light mainLight = GetMainLight();
                    half atten = 1.0h;
                #endif

                // The surface is flat and faces straight up, so the whole
                // directional term is the sun's height. Wrapped, so a low sun
                // dims the pool instead of switching it off.
                half sun = saturate((mainLight.direction.y + _LightWrap) / (1.0h + _LightWrap));
                col *= mainLight.color * sun * _SunStrength * atten + _AmbientColor.rgb;

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // The pool has to stay in the depth texture: other surfaces read camera
        // depth, and a hole where the water is would break them.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
