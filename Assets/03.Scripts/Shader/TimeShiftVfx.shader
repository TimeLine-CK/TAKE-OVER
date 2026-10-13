Shader "Hidden/TimeShiftVfx"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _SourceTex ("Captured World", 2D) = "black" {}
        _RevealMask ("Rift Boundary", 2D) = "black" {}
        _SourceFlipY ("Source Flip Y", Float) = 0
        _VideoTex ("Video", 2D) = "black" {}
        [HDR] _EffectColor ("Effect Color", Color) = (0.1, 1.4, 2.5, 1)
        _Frame ("Video Frame", Float) = 0
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
            TEXTURE2D(_RevealMask);
            SAMPLER(sampler_RevealMask);
            float4 _RevealMask_TexelSize;
            float4 _EffectColor;
            float _Frame;
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

                float2 offset = input.uv - 0.5;
                float angle = frac(atan2(offset.y, offset.x) / 6.28318530718 + 1.0);
                float row = 1.0 - (min(_Frame, _RevealMask_TexelSize.w - 1.0) + 0.5) * _RevealMask_TexelSize.y;
                float radius = SAMPLE_TEXTURE2D(_RevealMask, sampler_RevealMask, float2(angle, row)).r;
                float distanceFromCenter = length(offset);
                float feather = max(fwidth(distanceFromCenter), 0.001);
                half sourceAlpha = smoothstep(radius - feather, radius + feather, distanceFromCenter);
                if (_Frame < 0.5) sourceAlpha = 1.0;
                half effectAlpha = saturate(brightness * 2.5);
                half alpha = sourceAlpha + effectAlpha * (1.0 - sourceAlpha);
                half3 effect = _EffectColor.rgb * brightness * 2.0;
                half3 color = (source * sourceAlpha * (1.0 - effectAlpha) + effect) / max(alpha, 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
