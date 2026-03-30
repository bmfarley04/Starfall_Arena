Shader "Hidden/Starfall/3D/GigablastEdgeGlow"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "GigablastEdgeGlow"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(sampler_BlitTexture);

            float4 _GigablastEdgeGlow_EdgeColor;
            float4 _GigablastEdgeGlow_Params1;
            float4 _GigablastEdgeGlow_Params2;
            float4 _GigablastEdgeGlow_Params3;

            float BuildEdgeMask(float edgeDist, float thickness, float softness)
            {
                float outer = max(thickness, 0.0001);
                float inner = max(outer - max(softness, 0.0001), 0.0);
                return 1.0 - smoothstep(inner, outer, edgeDist);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                float charge = saturate(_GigablastEdgeGlow_Params1.x);
                if (charge <= 0.0)
                {
                    return sceneColor;
                }

                float coreThickness = lerp(max(_GigablastEdgeGlow_Params1.y, 0.0), max(_GigablastEdgeGlow_Params1.z, 0.0), charge);
                float coreSoftness = max(_GigablastEdgeGlow_Params1.w, 0.0001);

                float haloThickness = lerp(max(_GigablastEdgeGlow_Params2.x, 0.0), max(_GigablastEdgeGlow_Params2.y, 0.0), charge);
                float haloSoftness = max(_GigablastEdgeGlow_Params2.z, 0.0001);
                float coreIntensity = max(_GigablastEdgeGlow_Params2.w, 0.0);

                float haloIntensity = max(_GigablastEdgeGlow_Params3.x, 0.0);
                float cornerBoost = max(_GigablastEdgeGlow_Params3.y, 0.0);
                float horizontalBias = max(_GigablastEdgeGlow_Params3.z, 0.01);
                float verticalBias = max(_GigablastEdgeGlow_Params3.w, 0.01);

                float distLeft = uv.x;
                float distRight = 1.0 - uv.x;
                float distBottom = uv.y;
                float distTop = 1.0 - uv.y;

                float horizontalEdgeDist = min(distLeft, distRight) / horizontalBias;
                float verticalEdgeDist = min(distBottom, distTop) / verticalBias;
                float edgeDist = min(horizontalEdgeDist, verticalEdgeDist);

                float baseCoreMask = BuildEdgeMask(edgeDist, coreThickness, coreSoftness);
                float baseHaloMask = BuildEdgeMask(edgeDist, haloThickness, haloSoftness);

                float cornerOuter = max(haloThickness, coreThickness);
                float horizontalCornerMask = 1.0 - smoothstep(0.0, max(cornerOuter, 0.0001), min(distLeft, distRight));
                float verticalCornerMask = 1.0 - smoothstep(0.0, max(cornerOuter, 0.0001), min(distBottom, distTop));
                float cornerMask = horizontalCornerMask * verticalCornerMask;

                float cornerMultiplier = 1.0 + cornerMask * cornerBoost;

                float coreMask = saturate(baseCoreMask * cornerMultiplier);
                float haloMask = saturate(baseHaloMask * lerp(1.0, cornerMultiplier, 0.65));

                float chargeSquared = charge * charge;
                float3 glow =
                    _GigablastEdgeGlow_EdgeColor.rgb * coreMask * coreIntensity * charge +
                    _GigablastEdgeGlow_EdgeColor.rgb * haloMask * haloIntensity * chargeSquared;

                return half4(sceneColor.rgb + glow, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
