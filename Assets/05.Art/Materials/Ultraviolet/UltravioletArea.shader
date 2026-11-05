Shader "TAKEOVER/UltravioletArea"
{
    Properties
    {
        [HDR] _Color ("Light Color", Color) = (0.5, 0.16, 1.4, 1)
        [HDR] _CoreColor ("Core Color", Color) = (0.55, 0.45, 1.2, 1)
        _Opacity ("Intensity", Range(0, 1)) = 0.35
        _EdgeSoftness ("Edge Softness", Range(0.05, 0.9)) = 0.45
        _FlowStrength ("Flow Strength", Range(0, 0.3)) = 0.08
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            Blend SrcAlpha One
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                float _Opacity;
                float _EdgeSoftness;
                float _FlowStrength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 blend = frac(p);
                blend = blend * blend * (3 - 2 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), blend.x), blend.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 position = input.uv * 2 - 1;
                float2 fade = 1 - smoothstep(1 - _EdgeSoftness, 1, abs(position));
                float edge = fade.x * fade.y;
                float core = exp(-dot(position, position) * 4);
                float haze = Noise(input.uv * float2(5, 7) + float2(_Time.y * 0.05, -_Time.y * 0.08));
                float flow = lerp(1 - _FlowStrength, 1 + _FlowStrength, haze);
                half3 color = lerp(_Color.rgb, _CoreColor.rgb, core * 0.7);
                float alpha = _Opacity * edge * (0.3 + core * 0.7) * flow;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
