Shader "TAKEOVER/LightBeam"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _CoreBrightness ("Core Brightness", Range(0, 1)) = 0.6
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _CoreBrightness;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * _MainTex_ST.xy + _MainTex_ST.zw) * input.color;
                float across = abs(input.uv.y * 2 - 1);
                float edge = 1 - smoothstep(1 - _EdgeSoftness, 1, across);
                float core = 1 - smoothstep(0, 0.65, across);
                color.rgb = lerp(color.rgb, half3(1, 1, 1), core * _CoreBrightness);
                color.a *= edge;
                return color;
            }
            ENDHLSL
        }
    }
}
