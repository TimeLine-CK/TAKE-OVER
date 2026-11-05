Shader "TAKEOVER/CartoonWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.12, 0.58, 0.62, 0.88)
        _DeepColor ("Deep Color", Color) = (0.025, 0.19, 0.27, 0.94)
        _FoamColor ("Foam Color", Color) = (0.68, 0.96, 0.91, 1)
        _WaveHeight ("Wave Height", Range(0, 0.15)) = 0.055
        _WaveSpeed ("Wave Speed", Float) = 1.3
        _FoamWidth ("Foam Width", Range(0.005, 0.1)) = 0.028
        _WaterSize ("Water Size", Vector) = (8, 2, 0, 0)
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _ShallowColor;
            float4 _DeepColor;
            float4 _FoamColor;
            float4 _WaterSize;
            float _WaveHeight;
            float _WaveSpeed;
            float _FoamWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.world = world.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float height = max(_WaterSize.y, 0.001);
                float t = _Time.y * _WaveSpeed;
                float wave = 0.6 + sin(input.world.x * 3.4 + t) * 0.25
                    + sin(input.world.x * 6.7 - t * 0.7) * 0.15;
                float amplitude = min(_WaveHeight, height * 0.25);
                float distance = (1 - input.uv.y) * height - wave * amplitude;
                float aa = max(fwidth(distance), 0.0005);
                float mask = smoothstep(-aa, aa, distance);
                float foam = 1 - smoothstep(_FoamWidth, _FoamWidth + aa * 1.5, distance);

                float depth = saturate(distance / height);
                float band = floor((1 - depth) * 4) / 3;
                band = saturate(band);
                float4 body = lerp(_DeepColor, _ShallowColor, band);
                float light = 1 - smoothstep(0.035, 0.18, distance);
                body.rgb = lerp(body.rgb, _ShallowColor.rgb, light * 0.5);

                float ripple = pow(saturate(sin(input.world.y * 16
                    + sin(input.world.x * 1.7 + t) * 0.65 + t * 0.7)), 20);
                ripple *= smoothstep(0.2, 0.8, sin(input.world.x * 2.3 - t * 0.65));
                ripple *= smoothstep(0.08, 0.25, distance) * (1 - depth);
                body.rgb = lerp(body.rgb, _FoamColor.rgb, ripple * 0.13);

                float3 color = lerp(body.rgb, _FoamColor.rgb, foam);
                float alpha = lerp(body.a, _FoamColor.a, foam) * mask;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
