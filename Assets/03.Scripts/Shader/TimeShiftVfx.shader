Shader "Hidden/TimeShiftVfx"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _SourceTex ("Captured World", 2D) = "black" {}
        _SourceFlipY ("Source Flip Y", Float) = 0
        _VideoTex ("Video", 2D) = "black" {}
        _WorldBlend ("World Blend", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_VideoTex);
            SAMPLER(sampler_VideoTex);
            TEXTURE2D(_SourceTex);
            SAMPLER(sampler_SourceTex);
            float _WorldBlend;
            float _SourceFlipY;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 sourceUV = input.uv;
                sourceUV.y = lerp(sourceUV.y, 1.0 - sourceUV.y, _SourceFlipY);
                half3 source = SAMPLE_TEXTURE2D(_SourceTex, sampler_SourceTex, sourceUV).rgb;
                half3 video = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, input.uv).rgb;
                half brightness = max(video.r, max(video.g, video.b));

                half sourceAlpha = 1.0 - saturate(_WorldBlend);
                half effectAlpha = saturate(brightness);
                half alpha = sourceAlpha + effectAlpha * (1.0 - sourceAlpha);
                half3 effect = video;
                half3 color = (source * sourceAlpha * (1.0 - effectAlpha) + effect) / max(alpha, 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
