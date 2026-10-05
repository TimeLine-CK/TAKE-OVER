Shader "Custom/BloodMark"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Map", 2D) = "white" {}

        _BloodLayer ("Blood Layer", Range(0, 2)) = 0
    }

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

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            // =========================================================
            // Attributes
            // =========================================================

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };


            // =========================================================
            // Varyings
            // =========================================================

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float4 screenPos : TEXCOORD1;
            };


            // =========================================================
            // Base Texture
            // =========================================================

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);


            // =========================================================
            // Global Blood Masks
            // =========================================================

            TEXTURE2D(_BloodNearTexture);
            SAMPLER(sampler_BloodNearTexture);

            TEXTURE2D(_BloodMidTexture);
            SAMPLER(sampler_BloodMidTexture);

            TEXTURE2D(_BloodFarTexture);
            SAMPLER(sampler_BloodFarTexture);


            // =========================================================
            // Material
            // =========================================================

            CBUFFER_START(UnityPerMaterial)

                half4 _BaseColor;
                float4 _BaseMap_ST;

                float _BloodLayer;

            CBUFFER_END


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
                    TRANSFORM_TEX(
                        IN.uv,
                        _BaseMap
                    );

                OUT.color =
                    IN.color;

                OUT.screenPos =
                    ComputeScreenPos(
                        OUT.positionHCS
                    );

                return OUT;
            }


            // =========================================================
            // Fragment
            // =========================================================

            half4 frag(Varyings IN) : SV_Target
            {
                // =====================================================
                // 1. Original Color
                // =====================================================

                half4 color =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        IN.uv
                    );

                color *= _BaseColor;
                color *= IN.color;


                // =====================================================
                // 2. Screen UV
                // =====================================================

                float2 screenUV =
                    IN.screenPos.xy /
                    IN.screenPos.w;


                // =====================================================
                // 3. Current Layer Mask
                // =====================================================

                half currentMask = 0.0h;


                if (_BloodLayer < 0.5h)
                {
                    // Near
                    currentMask =
                        SAMPLE_TEXTURE2D(
                            _BloodNearTexture,
                            sampler_BloodNearTexture,
                            screenUV
                        ).r;
                }
                else if (_BloodLayer < 1.5h)
                {
                    // Mid
                    currentMask =
                        SAMPLE_TEXTURE2D(
                            _BloodMidTexture,
                            sampler_BloodMidTexture,
                            screenUV
                        ).r;
                }
                else
                {
                    // Far
                    currentMask =
                        SAMPLE_TEXTURE2D(
                            _BloodFarTexture,
                            sampler_BloodFarTexture,
                            screenUV
                        ).r;
                }


                // =====================================================
                // 4. 상위 Layer Mask 확인
                //
                // Near < Mid < Far
                //
                // Mid:
                //     Near가 있으면 숨김
                //
                // Far:
                //     Near 또는 Mid가 있으면 숨김
                // =====================================================

                half higherMask = 0.0h;


                // -----------------------------------------------------
                // Mid
                // -----------------------------------------------------

                if (_BloodLayer >= 0.5h &&
                    _BloodLayer < 1.5h)
                {
                    higherMask =
                        SAMPLE_TEXTURE2D(
                            _BloodNearTexture,
                            sampler_BloodNearTexture,
                            screenUV
                        ).r;
                }


                // -----------------------------------------------------
                // Far
                // -----------------------------------------------------

                else if (_BloodLayer >= 1.5h)
                {
                    half nearMask =
                        SAMPLE_TEXTURE2D(
                            _BloodNearTexture,
                            sampler_BloodNearTexture,
                            screenUV
                        ).r;

                    half midMask =
                        SAMPLE_TEXTURE2D(
                            _BloodMidTexture,
                            sampler_BloodMidTexture,
                            screenUV
                        ).r;

                    higherMask =
                        max(
                            nearMask,
                            midMask
                        );
                }


                // =====================================================
                // 5. 현재 Mask가 없는 곳은 투명
                // =====================================================

                color.a *= currentMask;


                // =====================================================
                // 6. 상위 Layer가 존재하면 투명
                // =====================================================

                if (higherMask > 0.5h)
                {
                    color.a = 0.0h;
                }


                return color;
            }

            ENDHLSL
        }
    }
}