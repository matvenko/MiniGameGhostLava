Shader "MazeBoo/MoonshardGem"
{
    Properties
    {
        _Glow("Glow", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float3 bary : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 bary : TEXCOORD2;
                half3 facet : COLOR;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.bary = input.bary;
                output.facet = input.color.rgb;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 l = normalize(half3(.45, .85, -.3));
                half diffuse = saturate(dot(n, l));
                half fresnel = pow(1.0h - saturate(dot(n, v)), 3.0h);
                half sparkle = pow(saturate(dot(reflect(-l, n), v)), 24.0h);
                // The facet's own colour carries the cut; light only nudges it
                // as the stone rocks, so the pattern never washes out.
                half3 colour = input.facet * (.85h + .2h * diffuse);
                colour += half3(.2h, .7h, .9h) * fresnel * .3h;
                // Bloom only catches what is brighter than 1, which the gold
                // coin's emission and specular reach and a plain facet colour
                // never did - so the coin glowed and the stone sat flat. Lifting
                // the whole stone puts the pale and light facets over that line
                // while the deep blues stay under it, and the cut still reads.
                colour *= _Glow;
                colour += half3(.8h, 1.0h, 1.0h) * sparkle * 1.6h;
                // A crisp pale hairline along every facet edge, about a pixel wide.
                float edge = min(input.bary.x, min(input.bary.y, input.bary.z));
                float hairline = 1.0 - smoothstep(0.0, fwidth(edge) * 1.3, edge);
                colour = lerp(colour, half3(.85h, 1.0h, 1.0h) * _Glow, hairline * .7h);
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }
    }
}
