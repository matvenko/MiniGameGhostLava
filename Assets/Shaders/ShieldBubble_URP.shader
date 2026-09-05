// The bubble the shield ability puts around the character for the five
// seconds nothing can touch it. Unlit and transparent for the same reason the
// freeze shell is: it has to read the same over dark grass and over the glow
// of the lava, so none of it comes from the scene lighting.
//
// Three things carry it. A fresnel rim, so the sphere is legible as a sphere
// from any angle and thickest at the silhouette where it matters. A grid of
// cells lit along their borders, which is what says "shield" rather than
// "soap bubble". And a band sweeping up through it, so a protected character
// is never standing inside a still image.
//
// Gold on purpose: the ice is blue and the teleport flare is cyan, and a
// player glancing at the board has to know which of the three they are
// looking at without reading anything.
Shader "Custom/ShieldBubble_URP"
{
    Properties
    {
        [HDR] _Color ("Cell Tint", Color) = (1, 0.78, 0.3, 0.3)
        [HDR] _RimColor ("Rim", Color) = (1, 0.93, 0.72, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.4
        _RimStrength ("Rim Strength", Range(0, 3)) = 1.4
        _CellScale ("Cell Count", Float) = 9
        _CellSharpness ("Cell Sharpness", Range(0.05, 0.5)) = 0.18
        _SweepSpeed ("Sweep Speed", Float) = 0.55
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // Both sides: with no depth write, seeing the far wall of the bubble
        // through the near one is what gives it volume.
        Cull Off
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            float4 _Color;
            float4 _RimColor;
            float _FresnelPower;
            float _RimStrength;
            float _CellScale;
            float _CellSharpness;
            float _SweepSpeed;
            float _Fade;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vpi.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(IN.viewDirWS);
                float3 normal = normalize(IN.normalWS);

                // abs, because the far wall comes through with its normal
                // pointing away and would otherwise read as solid rim.
                float rim = 1.0 - saturate(abs(dot(viewDir, normal)));
                float fresnel = pow(rim, _FresnelPower) * _RimStrength;

                // Two half-offset grids of cell centres; the distance to the
                // nearer one gives staggered cells rather than plain squares.
                // Bright along the borders, dim in the middle of each cell.
                float2 p = float2(IN.uv.x * _CellScale * 2.0, IN.uv.y * _CellScale);
                float2 a = frac(p) - 0.5;
                float2 b = frac(p + 0.5) - 0.5;
                float cell = sqrt(min(dot(a, a), dot(b, b)));
                float border = smoothstep(0.5 - _CellSharpness, 0.5, cell);

                // A band climbing the bubble, sharp enough to read as a pulse
                // travelling through the shield rather than as a gradient.
                float sweep = saturate(sin((IN.uv.y * 2.0 - _Time.y * _SweepSpeed) * 6.2831));
                sweep = pow(sweep, 6.0) * 0.45;

                half3 color = _Color.rgb * (0.25 + border * 0.85) + _RimColor.rgb * (fresnel + sweep);
                half alpha = saturate(_Color.a * (0.12 + border) + fresnel * 0.7 + sweep * 0.5) * _Fade;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
