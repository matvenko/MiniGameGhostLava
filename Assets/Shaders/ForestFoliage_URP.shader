Shader "Forest/Foliage URP"
{
    Properties
    {
        _BaseMap("Painted texture",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Cutoff("Alpha cutoff",Range(0,1))=.4
        _AlphaClip("Cutout",Float)=1
        _Cull("Cull",Float)=0
        _AmbientColor("Forest ambient",Color)=(.35,.43,.36,1)
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _AmbientColor;
            float _Cutoff, _AlphaClip, _Cull;
        CBUFFER_END
        struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
        Varyings Vert(Attributes v)
        {
            Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);
            o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
            o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.fog=ComputeFogFactor(o.positionCS.z);return o;
        }
        half4 Albedo(Varyings i){half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;clip(c.a-(_AlphaClip>.5?_Cutoff:-1));return c;}
        ENDHLSL
        Pass
        {
            Name "ForestForward"
            Tags{"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);half3 base=Albedo(i).rgb;
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half diffuse=saturate((dot(normalize(i.normalWS),light.direction)+.45)/1.45);
                half3 lit=base*(_AmbientColor.rgb+light.color*diffuse*lerp(.4,1,light.shadowAttenuation));
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            float3 _LightDirection;
            Varyings ShadowVert(Attributes v)
            {
                Varyings o=Vert(v);
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,_LightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 DepthFrag(Varyings i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);Albedo(i);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode"="DepthOnly"}
            ColorMask 0 ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);Albedo(i);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode"="DepthNormalsOnly"}
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            half4 NormalFrag(Varyings i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);Albedo(i);return half4(normalize(i.normalWS),0);}
            ENDHLSL
        }
    }
}
