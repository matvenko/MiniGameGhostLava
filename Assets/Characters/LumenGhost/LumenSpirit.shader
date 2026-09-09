Shader "MiniGame/LumenSpirit"
{
    Properties
    {
        _BaseColor("Surface", Color) = (0.83,0.94,1,1)
        _ShadeColor("Shaded surface", Color) = (0.35,0.65,0.76,1)
        [HDR] _GlowColor("Edge light", Color) = (0.2,2.5,3.5,1)
        _Emission("Self illumination", Range(0,2)) = 0.4
        _RimPower("Edge width", Range(1,8)) = 3
        _Gloss("Highlight", Range(0,1)) = 0.15
        _Dissolve("Visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShadeColor, _GlowColor;
            float _Emission, _RimPower, _Gloss, _Dissolve;
            CBUFFER_END
            struct Attributes { float4 position:POSITION; float3 normal:NORMAL; };
            struct Varyings { float4 position:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float3 local:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.world=TransformObjectToWorld(v.position.xyz);
                o.position=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.local=v.position.xyz;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float grain=frac(sin(dot(floor(i.local*7000),float3(12.9898,78.233,37.719)))*43758.5453);
                clip(_Dissolve-.001-grain*.998);
                half3 n=normalize(i.normal), v=normalize(GetWorldSpaceViewDir(i.world));
                half3 key=normalize(half3(-.4,.8,.5));
                half wrap=saturate(dot(n,key)*.5+.5);
                half rim=pow(1-saturate(dot(n,v)),_RimPower);
                half spec=pow(saturate(dot(n,normalize(key+v))),40)*_Gloss;
                half3 color=lerp(_ShadeColor.rgb,_BaseColor.rgb,wrap);
                color+=_BaseColor.rgb*_Emission+_GlowColor.rgb*rim+spec;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
