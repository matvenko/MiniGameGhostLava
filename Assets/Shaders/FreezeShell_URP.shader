// The block of ice an enemy is encased in while it is frozen. Unlit and
// transparent on purpose: the shell has to read the same on a ghost lit from
// the front and a ghoul standing in the lava glow, so none of it comes from
// the scene lighting.
//
// Three things make it read as ice rather than as a blue bubble: the facets of
// the mesh are flat-shaded and each carries its own brightness in vertex colour
// (same trick as the ground surface), a fresnel rim lights the silhouette so
// the shape is legible against any background, and a slow band of glint travels
// up through it so a frozen enemy is never a dead still image.
Shader "Custom/FreezeShell_URP"
{
    Properties
    {
        [HDR] _Color ("Ice Tint", Color) = (0.45, 0.78, 1, 0.34)
        [HDR] _RimColor ("Rim", Color) = (0.82, 0.97, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 3)) = 1.35
        _FacetVariation ("Facet Variation", Range(0, 1)) = 0.4
        _GlintSpeed ("Glint Speed", Float) = 0.9
        _GlintScale ("Glint Scale", Float) = 5
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // Both sides: with no depth write, seeing the far wall of the shell
        // through the near one is what gives it thickness, and it means the
        // generated mesh's winding can never cull the whole thing away.
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
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float4 color : COLOR;
            };

            float4 _Color;
            float4 _RimColor;
            float _FresnelPower;
            float _RimStrength;
            float _FacetVariation;
            float _GlintSpeed;
            float _GlintScale;
            float _Fade;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vpi.positionWS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(IN.viewDirWS);
                float3 normal = normalize(IN.normalWS);

                // abs, because back faces come through with their normals
                // pointing away and would otherwise read as solid rim.
                float rim = 1.0 - saturate(abs(dot(viewDir, normal)));
                float fresnel = pow(rim, _FresnelPower) * _RimStrength;

                // Per-facet brightness, baked into vertex colour when the shell
                // was built, so every face catches the light differently.
                float facet = lerp(1.0 - _FacetVariation, 1.0 + _FacetVariation, IN.color.r);

                // A band of glint sliding up the shell. Driven off world height
                // rather than UVs - the mesh is generated and has none worth
                // relying on.
                float glint = sin(IN.positionWS.y * _GlintScale - _Time.y * _GlintSpeed * 6.2831);
                glint = pow(saturate(glint), 8.0) * 0.35;

                half3 color = _Color.rgb * facet + _RimColor.rgb * (fresnel + glint);
                half alpha = saturate(_Color.a * facet + fresnel * 0.55 + glint * 0.4) * _Fade;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
