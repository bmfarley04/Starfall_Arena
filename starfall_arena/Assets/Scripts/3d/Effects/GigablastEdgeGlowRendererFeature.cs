using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class GigablastEdgeGlowRendererFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/Starfall/3D/GigablastEdgeGlow";
    private const string PassName = "Gigablast Edge Glow";

    private Material _material;
    private GigablastEdgeGlowRenderPass _pass;

    public override void Create()
    {
        _pass ??= new GigablastEdgeGlowRenderPass();
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        if (_material != null)
        {
            return;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"GigablastEdgeGlowRendererFeature could not find shader \"{ShaderName}\".");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(shader);
        _material.hideFlags = HideFlags.HideAndDontSave;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!IsAnyEdgeGlowVisible())
        {
            return;
        }

        CameraType cameraType = renderingData.cameraData.cameraType;
        if (cameraType != CameraType.Game)
        {
            return;
        }

        if (_material == null)
        {
            Create();
            if (_material == null)
            {
                return;
            }
        }

        _pass.Setup(_material);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        _material = null;
    }

    private sealed class GigablastEdgeGlowRenderPass : ScriptableRenderPass
    {
        private Material _material;

        public void Setup(Material material)
        {
            _material = material;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || !IsAnyEdgeGlowVisible())
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.camera.cameraType != CameraType.Game)
            {
                return;
            }

            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = "_CameraColorGigablastEdgeGlow";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);
            RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(parameters, PassName);

            resourceData.cameraColor = destination;
        }
    }

    private static bool IsAnyEdgeGlowVisible()
    {
        return GigablastChargeEdgeGlow3D.IsEffectVisible;
    }
}
