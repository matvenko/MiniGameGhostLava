// The flare left behind when the character teleports out of a tile, and the
// one that gathers over the tile it arrives on. Unlit and additive on purpose:
// it has to read as light rather than as a surface, over a board that is half
// dark grass and half glowing lava, so none of it comes from scene lighting.
//
// The shape is all in the generated mesh (a ground ring and a column of light);
// what this adds is the life in it. Brightness comes from vertex colour, which
// is where the mesh's soft edges live, and two travelling bands - one around
// the ring, one up the column - keep the flare from being a static decal for
// the half second it is on screen.
Shader "Custom/TeleportFlare_URP"
{
    Properties
    {
        [HDR] _Color ("Tint", Color) = (0.42, 0.86, 1, 1)
        [HDR] _CoreColor ("Core", Color) = (0.85, 0.98, 1, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _SpinSpeed ("Spin Speed", Float) = 3
        _SpinScale ("Spin Bands", Float) = 7
        _RiseSpeed ("Rise Speed", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        // Additive: the flare brightens whatever is behind it and never
        // darkens it, which is what keeps it reading as light on both the
        // grass and the lava.
        Blend SrcAlpha One
        ZWrite Off
        // Both sides, since the ring is a flat sheet and the column is an open
        // tube - either can be seen from behind as the camera swings around.
        Cull Off
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float4 _Color;
            float4 _CoreColor;
            float _Fade;
            float _SpinSpeed;
            float _SpinScale;
            float _RiseSpeed;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = GetVertexPositionInputs(IN.positionOS.xyz).positionCS;
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // u is the angle round the ring or the column, v is across the
                // band or up the column - the mesh writes both.
                float spin = sin((IN.uv.x * _SpinScale - _Time.y * _SpinSpeed) * 6.2831);
                spin = 0.72 + 0.28 * spin;

                // A second band climbing the column, so the light looks like it
                // is being drawn up out of the floor rather than just standing
                // there. Flat on the ring, where v barely varies.
                float rise = sin((IN.uv.y * 2.5 - _Time.y * _RiseSpeed) * 6.2831);
                rise = 0.85 + 0.15 * saturate(rise);

                float energy = IN.color.a * spin * rise;

                // The hottest part of the flare goes white rather than just
                // brighter blue, the way a light source does.
                half3 color = lerp(_Color.rgb, _CoreColor.rgb, saturate(energy * energy));

                return half4(color, saturate(energy * _Color.a) * _Fade);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
