using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class BlackHoleLensSourceRendererFeature : ScriptableRendererFeature
{
    private const string PassName = "Black Hole Lens Source";
    private static readonly int LensSourceTextureId = Shader.PropertyToID("_BlackHoleLensSourceTexture");
    private static readonly int UseLensSourceTextureId = Shader.PropertyToID("_BlackHoleUseLensSourceTexture");

    [Tooltip("Only renderers on these layers are written into the black-hole lens source texture. Keep gameplay ships, UI, cursors, and normal asteroids out of this mask.")]
    [SerializeField] private LayerMask lensSourceLayerMask;

    [Tooltip("Render pass timing for capturing lensable sources. Use After Rendering Opaques so opaque/cutout accretion disks are available before the transparent singularity lens renders.")]
    [SerializeField] private RenderPassEvent captureEvent = RenderPassEvent.AfterRenderingOpaques;

    [Tooltip("Render transparent objects into the lens source as well as opaque/cutout objects. Leave disabled unless a deliberately lensable transparent VFX layer needs to be bent.")]
    [SerializeField] private bool includeTransparentObjects;

    private BlackHoleLensSourcePass _pass;

    public override void Create()
    {
        _pass ??= new BlackHoleLensSourcePass();
        _pass.renderPassEvent = captureEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
        {
            return;
        }

        if (lensSourceLayerMask.value == 0)
        {
            Shader.SetGlobalFloat(UseLensSourceTextureId, 0f);
            return;
        }

        _pass ??= new BlackHoleLensSourcePass();
        _pass.renderPassEvent = captureEvent;
        _pass.Setup(lensSourceLayerMask, includeTransparentObjects);
        renderer.EnqueuePass(_pass);
    }

    private sealed class BlackHoleLensSourcePass : ScriptableRenderPass
    {
        private readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        private FilteringSettings _opaqueFilteringSettings;
        private FilteringSettings _transparentFilteringSettings;
        private bool _includeTransparentObjects;

        public void Setup(LayerMask layerMask, bool includeTransparentObjects)
        {
            _opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            _transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, layerMask);
            _includeTransparentObjects = includeTransparentObjects;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            if (cameraData.camera.cameraType != CameraType.Game || resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle activeColor = resourceData.activeColorTexture;
            if (!activeColor.IsValid())
            {
                return;
            }

            TextureDesc lensSourceDesc = renderGraph.GetTextureDesc(activeColor);
            lensSourceDesc.name = "_BlackHoleLensSourceTexture";
            lensSourceDesc.format = GraphicsFormat.R16G16B16A16_SFloat;
            lensSourceDesc.clearBuffer = true;
            lensSourceDesc.clearColor = Color.clear;

            TextureHandle lensSourceTexture = renderGraph.CreateTexture(lensSourceDesc);
            RendererListHandle opaqueRendererList = CreateRendererList(renderGraph, renderingData, cameraData, lightData, _opaqueFilteringSettings, cameraData.defaultOpaqueSortFlags);
            RendererListHandle transparentRendererList = default;
            if (_includeTransparentObjects)
            {
                transparentRendererList = CreateRendererList(renderGraph, renderingData, cameraData, lightData, _transparentFilteringSettings, SortingCriteria.CommonTransparent);
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out PassData passData))
            {
                passData.opaqueRendererList = opaqueRendererList;
                passData.transparentRendererList = transparentRendererList;
                passData.drawTransparentObjects = _includeTransparentObjects;

                builder.SetRenderAttachment(lensSourceTexture, 0, AccessFlags.WriteAll);
                builder.UseRendererList(opaqueRendererList);
                if (_includeTransparentObjects)
                {
                    builder.UseRendererList(transparentRendererList);
                }

                builder.SetGlobalTextureAfterPass(lensSourceTexture, LensSourceTextureId);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(false, true, Color.clear);
                    context.cmd.DrawRendererList(data.opaqueRendererList);
                    if (data.drawTransparentObjects)
                    {
                        context.cmd.DrawRendererList(data.transparentRendererList);
                    }

                    context.cmd.SetGlobalFloat(UseLensSourceTextureId, 1f);
                });
            }
        }

        private RendererListHandle CreateRendererList(
            RenderGraph renderGraph,
            UniversalRenderingData renderingData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            FilteringSettings filteringSettings,
            SortingCriteria sortingCriteria)
        {
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
            RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            return renderGraph.CreateRendererList(rendererListParams);
        }

        private sealed class PassData
        {
            public RendererListHandle opaqueRendererList;
            public RendererListHandle transparentRendererList;
            public bool drawTransparentObjects;
        }
    }
}
