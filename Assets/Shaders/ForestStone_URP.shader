Shader "Forest/Mossy Stone URP"
{
    Properties
    {
        _Masonry("Bevelled masonry geometry",Float)=1
        _StoneColor("Stone",Color)=(.48,.52,.48,1)
        _MossColor("Moss",Color)=(.22,.36,.09,1)
        _MossCoverage("Moss coverage",Range(0,1))=.5
        _AmbientColor("Ambient",Color)=(.52,.57,.56,1)
        _StoneMap("Stone grain",2D)="white"{}
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _StoneColor,_MossColor,_AmbientColor,_StoneMap_ST;float _Masonry,_MossCoverage;
        CBUFFER_END
        TEXTURE2D(_StoneMap);SAMPLER(sampler_StoneMap);
        struct A{float4 p:POSITION;float3 n:NORMAL;half4 c:COLOR;};
        struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;half3 n:TEXCOORD1;half4 c:TEXCOORD2;half fog:TEXCOORD3;};
        V Vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.fog=ComputeFogFactor(o.p.z);return o;}
        float Hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
        float Noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);}
        ENDHLSL
        Pass
        {
            Tags{"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            half4 Frag(V i):SV_Target
            {
                half3 n=normalize(i.n);float coarse=Noise(i.world*2.8),fine=Noise(i.world*32);
                half moss=smoothstep(.60,.85,coarse+max(0,n.y)*.12+i.c.g*.1+fine*.12)*_MossCoverage;
                half3 weights=pow(abs(n),4);weights/=max(dot(weights,1),.001);
                half3 grainColor=SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,i.world.yz*.8).rgb*weights.x
                          +SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,i.world.xz*.8).rgb*weights.y
                          +SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,i.world.xy*.8).rgb*weights.z;
                half3 albedo=lerp(lerp(_StoneColor.rgb,grainColor*1.45,.68)*(.85+i.c.r*.28),_MossColor.rgb*(.8+fine*.4),moss);
                float grain=dot(grainColor,half3(.3,.59,.11));
                float3 dx=ddx(i.world),dy=ddy(i.world),r1=cross(dy,n),r2=cross(n,dx);
                float determinant=dot(dx,r1);
                n=normalize(n-.07*sign(determinant)*(ddx(grain)*r1+ddy(grain)*r2)/max(abs(determinant),.00001));
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                half diffuse=saturate((dot(n,light.direction)+.35)/1.35);
                half3 col=albedo*(_AmbientColor.rgb+light.color*diffuse*lerp(.25,1,light.shadowAttenuation));
                return half4(MixFog(col,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Tags{"LightMode"="ShadowCaster"}ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Empty
            float3 _LightDirection;
            V ShadowVert(A a){V o=Vert(a);o.p=TransformWorldToHClip(ApplyShadowBias(o.world,o.n,_LightDirection));
                #if UNITY_REVERSED_Z
                o.p.z=min(o.p.z,UNITY_NEAR_CLIP_VALUE);
                #else
                o.p.z=max(o.p.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;}
            half4 Empty(V i):SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Tags{"LightMode"="DepthOnly"}ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Empty
            half4 Empty(V i):SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Tags{"LightMode"="DepthNormalsOnly"}ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normal
            half4 Normal(V i):SV_Target{return half4(normalize(i.n),0);}
            ENDHLSL
        }
    }
}
