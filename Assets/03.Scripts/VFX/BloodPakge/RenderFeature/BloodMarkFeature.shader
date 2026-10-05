Shader "Hidden/BloodMask"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "Universal2D"
            }

            // Mask Texture에 직접 기록
            Blend One Zero

            // 기존 Depth가 있다면 그것을 기준으로 가시성 테스트
            ZWrite Off
            ZTest LEqual

            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // =========================================================
            // Sprite Texture
            // =========================================================

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);


            // =========================================================
            // SpriteRenderer Color
            // =========================================================

            CBUFFER_START(UnityPerMaterial)

                half4 _Color;

            CBUFFER_END


            // =========================================================
            // Attributes
            // =========================================================

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color      : COLOR;
            };


            // =========================================================
            // Varyings
            // =========================================================

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv           : TEXCOORD0;
                half4 color        : COLOR;
            };


            // =========================================================
            // Vertex
            // =========================================================

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS =
                    TransformObjectToHClip(
                        IN.positionOS.xyz
                    );

                OUT.uv =
                    IN.uv;

                OUT.color =
                    IN.color;

                return OUT;
            }


            // =========================================================
            // Fragment
            // =========================================================

            half4 frag(Varyings IN) : SV_Target
            {
                // -----------------------------------------------------
                // Sprite Texture
                // -----------------------------------------------------

                half4 sprite =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        IN.uv
                    );


                // -----------------------------------------------------
                // Sprite 실제 Alpha
                // -----------------------------------------------------

                half alpha =
                    sprite.a *
                    IN.color.a *
                    _Color.a;

                // -----------------------------------------------------
                // Sprite가 없는 부분 제거
                //
                // Anti-Aliasing으로 생기는 반투명 경계도
                // Mask에는 포함하지 않는다.
                // -----------------------------------------------------

                clip(alpha - 0.5h);

                // -----------------------------------------------------
                // 실제 Sprite 영역
                // WHITE = Mask 1
                // -----------------------------------------------------

                return half4(
                    1.0h,
                    1.0h,
                    1.0h,
                    1.0h
                );
            }

            ENDHLSL
        }
    }
}