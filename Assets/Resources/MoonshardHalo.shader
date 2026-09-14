Shader "MazeBoo/MoonshardHalo"
{
    Properties
    {
        _Color("Color", Color) = (.35,.9,1,.55)
        _Power("Falloff", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Power;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            // A soft round blob: the gem's glow on the grass, or its shadow.
            half4 frag(Varyings input) : SV_Target
            {
                float r = length(input.uv * 2 - 1);
                return half4(_Color.rgb, pow(saturate(1 - r), _Power) * _Color.a);
            }
            ENDHLSL
        }
    }
}
