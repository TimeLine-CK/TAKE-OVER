Shader "TAKEOVER/SlashRibbon"
{
    Properties
    {
        _Emission("Emission", Range(0, 8)) = 2.4
        [HDR] _BodyColor("Body Color", Color) = (0.01, 0.035, 0.1, 0.8)
        [HDR] _BladeColor("Blade Color", Color) = (0.02, 0.55, 1.8, 0.9)
        [HDR] _EdgeColor("Edge Color", Color) = (0.8, 1.8, 3, 1)
        [HideInInspector] _Progress("Progress", Float) = 0
        [HideInInspector] _EffectAge("Effect Age", Float) = 0
        [HideInInspector] _Seed("Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 layer : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float layer : TEXCOORD1;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor;
                half4 _BladeColor;
                half4 _EdgeColor;
                float _Emission;
                float _Progress;
                float _EffectAge;
                float _Seed;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.layer = input.layer.x;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 tint = input.layer < 0.5
                    ? _BodyColor
                    : input.layer < 1.5 ? _BladeColor : _EdgeColor;
                float pulse = 0.96 + sin(_EffectAge * 36 + _Seed * 1.7 + _Progress * 4) * 0.04;
                float tip = smoothstep(0, 0.04, input.uv.x)
                    * (1 - smoothstep(0.96, 1, input.uv.x));
                half alpha = saturate(tint.a * input.color.a * tip);
                return half4(tint.rgb * max(0, _Emission) * pulse, alpha);
            }
            ENDHLSL
        }
    }
}
