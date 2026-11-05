Shader "TAKEOVER/GasFog"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0.25, 0.32, 0.85, 0.6)
        _HighlightColor ("Wisps", Color) = (0.45, 0.72, 1, 1)
        _Density ("Density", Range(0,1)) = 0
        [Toggle] _Cone ("Steam Cone", Float) = 0
        _FlowSpeed ("Flow Speed", Float) = 0.12
        _NoiseScale ("Noise Scale", Float) = 1.3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _HighlightColor;
            float _Density, _FlowSpeed, _NoiseScale, _Cone;
            CBUFFER_END
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float fbm(float2 p)
            {
                float value=0, amplitude=0.5;
                for(int i=0;i<4;i++) { value+=noise(p)*amplitude; p=p*2.03+7.17; amplitude*=0.5; }
                return value;
            }
            Varyings vert(Attributes a)
            {
                Varyings o; float3 world=TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world); o.uv=a.uv; o.world=world.xy; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float t=_Time.y*_FlowSpeed;
                float2 p=i.world*_NoiseScale;
                float2 flow=float2(-t,t*0.25);
                if (_Cone > 0.5)
                {
                    p=i.uv*float2(2,4)*_NoiseScale;
                    flow=float2(t*0.08,-t*2);
                }
                float warp=fbm(p*0.65+float2(t,-t*0.4));
                float n=fbm(p+flow+warp*1.7);
                float ribbon=smoothstep(0.45,0.85,n);
                float2 edge=min(i.uv,1-i.uv);
                float feather=smoothstep(0,0.035,edge.x)*smoothstep(0,0.05,edge.y);
                if (_Cone > 0.5)
                {
                    float2 plume=i.uv*float2(5,7)+float2(t*0.12,-t*2.4);
                    float billow=fbm(plume+warp*0.8);
                    float detail=noise(plume*3.1+float2(t,-t*0.7));
                    float drift=(noise(float2(i.uv.y*4-t*0.6,t*0.3))-0.5)*0.08*i.uv.y;
                    float side=(1-i.uv.y)*0.5-abs(i.uv.x-0.5-drift);
                    float outline=side+(billow-0.5)*0.18+(detail-0.5)*0.035-0.025;
                    float wisps=lerp(0.3,1,smoothstep(0.22,0.72,billow));
                    feather=smoothstep(-0.035,0.075,outline)
                        *smoothstep(0,0.06,i.uv.y)
                        *(1-smoothstep(0.65,1,i.uv.y))
                        *smoothstep(0,0.045,edge.x);
                    feather*=lerp(wisps,1,saturate(side*8)*0.5);
                }
                float alpha=_Color.a*_Density*feather*(0.48+n*0.7);
                float3 color=lerp(_Color.rgb,_HighlightColor.rgb,ribbon*0.28);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
