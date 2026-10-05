using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class BloodRenderFeature : ScriptableRendererFeature
{
    // ================================================================
    // Settings
    // ================================================================

    [System.Serializable]
    public class BloodRenderSettings
    {
        public LayerMask BloodLayerNear;
        public LayerMask BloodLayerMid;
        public LayerMask BloodLayerFar;

        [Header("Mask Shader")]
        public Shader MaskShader;
    }


    // ================================================================
    // Global Texture IDs
    // ================================================================

    private static readonly int BloodNearTextureID =
        Shader.PropertyToID("_BloodNearTexture");

    private static readonly int BloodMidTextureID =
        Shader.PropertyToID("_BloodMidTexture");

    private static readonly int BloodFarTextureID =
        Shader.PropertyToID("_BloodFarTexture");


    // ================================================================
    // Pass
    // ================================================================

    private class BloodRenderPass : ScriptableRenderPass
    {
        private readonly BloodRenderSettings _settings;


        public BloodRenderPass(BloodRenderSettings settings)
        {
            _settings = settings;
        }


        // ============================================================
        // Pass Data
        // ============================================================

        private class PassData
        {
            public RendererListHandle RendererList;
        }


        // ============================================================
        // Record Render Graph
        // ============================================================

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            UniversalResourceData resourceData =
                frameData.Get<UniversalResourceData>();

            UniversalCameraData cameraData =
                frameData.Get<UniversalCameraData>();

            UniversalRenderingData renderingData =
                frameData.Get<UniversalRenderingData>();

            UniversalLightData lightData =
                frameData.Get<UniversalLightData>();


            // ========================================================
            // Near
            // ========================================================

            TextureHandle nearTexture =
                CreateBloodTexture(
                    renderGraph,
                    cameraData,
                    "Blood Near"
                );

            CreateBloodPass(
                renderGraph,
                resourceData,
                nearTexture,
                renderingData,
                cameraData,
                lightData,
                _settings.BloodLayerNear,
                BloodNearTextureID,
                "Blood Near"
            );


            // ========================================================
            // Mid
            // ========================================================

            TextureHandle midTexture =
                CreateBloodTexture(
                    renderGraph,
                    cameraData,
                    "Blood Mid"
                );

            CreateBloodPass(
                renderGraph,
                resourceData,
                midTexture,
                renderingData,
                cameraData,
                lightData,
                _settings.BloodLayerMid,
                BloodMidTextureID,
                "Blood Mid"
            );


            // ========================================================
            // Far
            // ========================================================

            TextureHandle farTexture =
                CreateBloodTexture(
                    renderGraph,
                    cameraData,
                    "Blood Far"
                );

            CreateBloodPass(
                renderGraph,
                resourceData,
                farTexture,
                renderingData,
                cameraData,
                lightData,
                _settings.BloodLayerFar,
                BloodFarTextureID,
                "Blood Far"
            );
        }


        // ============================================================
        // Create Mask Texture
        // ============================================================

        private TextureHandle CreateBloodTexture(
            RenderGraph renderGraph,
            UniversalCameraData cameraData,
            string textureName)
        {
            RenderTextureDescriptor descriptor =
                cameraData.cameraTargetDescriptor;


            // --------------------------------------------------------
            // Resolution
            // --------------------------------------------------------

            descriptor.width =
                cameraData.camera.pixelWidth;

            descriptor.height =
                cameraData.camera.pixelHeight;


            // --------------------------------------------------------
            // MSAA
            // --------------------------------------------------------

            descriptor.msaaSamples = 1;


            // --------------------------------------------------------
            // Depth
            //
            // 이 Texture 자체에는 Depth를 만들지 않는다.
            // 기존 Camera Depth를 사용한다.
            // --------------------------------------------------------

            descriptor.depthBufferBits = 0;


            // --------------------------------------------------------
            // R8 Mask
            // --------------------------------------------------------

            descriptor.graphicsFormat =
                GraphicsFormat.R8_UNorm;


            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.bindMS = false;


            return UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                textureName,
                true
            );
        }


        // ============================================================
        // Create Blood Pass
        // ============================================================

        private void CreateBloodPass(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            TextureHandle bloodTexture,
            UniversalRenderingData renderingData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            LayerMask layer,
            int globalTextureID,
            string passName)
        {
            // --------------------------------------------------------
            // Layer Filter
            // --------------------------------------------------------

            FilteringSettings filteringSettings =
                new FilteringSettings(
                    RenderQueueRange.all,
                    layer
                );


            // --------------------------------------------------------
            // 2D Renderer
            // --------------------------------------------------------

            DrawingSettings drawingSettings =
                RenderingUtils.CreateDrawingSettings(
                    new ShaderTagId("Universal2D"),
                    renderingData,
                    cameraData,
                    lightData,
                    SortingCriteria.CommonTransparent
                );


            drawingSettings.perObjectData =
                PerObjectData.None;


            // --------------------------------------------------------
            // Override Shader
            // --------------------------------------------------------

            if (_settings.MaskShader != null)
            {
                drawingSettings.overrideShader =
                    _settings.MaskShader;

                drawingSettings.overrideShaderPassIndex =
                    0;
            }


            // --------------------------------------------------------
            // Renderer List
            // --------------------------------------------------------

            RendererListParams rendererListParams =
                new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings
                );


            RendererListHandle rendererList =
                renderGraph.CreateRendererList(
                    rendererListParams
                );


            // --------------------------------------------------------
            // Raster Pass
            // --------------------------------------------------------

            using (
                var builder =
                    renderGraph.AddRasterRenderPass<PassData>(
                        passName,
                        out PassData passData
                    )
            )
            {
                passData.RendererList =
                    rendererList;


                builder.UseRendererList(
                    passData.RendererList
                );


                // ----------------------------------------------------
                // Color Attachment
                // ----------------------------------------------------

                builder.SetRenderAttachment(
                    bloodTexture,
                    0,
                    AccessFlags.Write
                );


                // ----------------------------------------------------
                // 기존 Camera Depth
                //
                // Depth를 새로 생성하지 않고
                // Camera Depth를 읽는다.
                // ----------------------------------------------------

                if (resourceData.cameraDepthTexture.IsValid())
                {
                    builder.SetInputAttachment(
                        resourceData.cameraDepthTexture,
                        0,
                        AccessFlags.Read
                    );
                }


                // ----------------------------------------------------
                // Global Texture
                // ----------------------------------------------------

                builder.SetGlobalTextureAfterPass(
                    bloodTexture,
                    globalTextureID
                );


                // ----------------------------------------------------
                // Pass Culling 방지
                // ----------------------------------------------------

                builder.AllowPassCulling(false);


                // ----------------------------------------------------
                // Execute
                // ----------------------------------------------------

                builder.SetRenderFunc(
                    static (
                        PassData data,
                        RasterGraphContext context
                    ) =>
                    {
                        // ============================================
                        // Background = BLACK
                        // ============================================

                        context.cmd.ClearRenderTarget(
                            false,
                            true,
                            Color.black
                        );


                        // ============================================
                        // Visible Layer = WHITE
                        // ============================================

                        context.cmd.DrawRendererList(
                            data.RendererList
                        );
                    }
                );
            }
        }
    }


    // ================================================================
    // Feature
    // ================================================================

    [SerializeField]
    private BloodRenderSettings Settings;

    private BloodRenderPass _bloodRenderPass;


    // ================================================================
    // Create
    // ================================================================

    public override void Create()
    {
        _bloodRenderPass =
            new BloodRenderPass(Settings);

        _bloodRenderPass.renderPassEvent =
            RenderPassEvent.AfterRenderingOpaques;
    }


    // ================================================================
    // Add Pass
    // ================================================================

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        renderer.EnqueuePass(
            _bloodRenderPass
        );
    }
}