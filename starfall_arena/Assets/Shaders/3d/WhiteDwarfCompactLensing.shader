Shader "Starfall/3D/WhiteDwarf/CompactLensing"
{
    Properties
    {
        [Header(Screen Space Lensing)]
        _ObjectRadius("Mesh Local Radius", Float) = 0.5
        _LensOpacity("Lens Opacity", Range(0.0, 1.0)) = 0.28
        _BendStrength("Bend Strength", Range(-0.35, 0.35)) = -0.055
        _BendFalloff("Bend Falloff", Range(0.25, 8.0)) = 2.2
        _BendClamp("Bend Clamp", Range(0.02, 0.6)) = 0.18
        _OuterFade("Outer Lens Fade", Range(0.001, 0.65)) = 0.32
        _ChromaticAberration("Chromatic Edge Split", Range(0.0, 0.035)) = 0.004

        [Header(Edge Glow)]
        [HDR]_LensGlowColor("Lens Glow HDR Color", Color) = (0.7, 2.2, 6.5, 1.0)
        _LensGlowIntensity("Lens Glow Intensity", Range(0.0, 8.0)) = 0.85
        _LensGlowPower("Lens Glow Power", Range(0.25, 12.0)) = 4.0
        _ExternalPulseIntensity("External Pulse Intensity", Range(0.0, 4.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WhiteDwarfCompactLensing"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 centerAndRadius : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _ObjectRadius;
                half _LensOpacity;
                float _BendStrength;
                float _BendFalloff;
                float _BendClamp;
                float _OuterFade;
                float _ChromaticAberration;
                half4 _LensGlowColor;
                half _LensGlowIntensity;
                half _LensGlowPower;
                half _ExternalPulseIntensity;
            CBUFFER_END

            float2 ScreenUvFromClip(float4 positionHCS)
            {
                float4 screenPosition = ComputeScreenPos(positionHCS);
                return screenPosition.xy / max(screenPosition.w, 0.00001);
            }

            float ProjectedRadius(float2 centerUv)
            {
                float objectRadius = max(_ObjectRadius, 0.0001);

                float2 xUv = ScreenUvFromClip(TransformObjectToHClip(float3(objectRadius, 0.0, 0.0)));
                float2 yUv = ScreenUvFromClip(TransformObjectToHClip(float3(0.0, objectRadius, 0.0)));
                float2 zUv = ScreenUvFromClip(TransformObjectToHClip(float3(0.0, 0.0, objectRadius)));

                float aspect = _ScreenParams.x / max(_ScreenParams.y, 0.0001);
                float2 aspectScale = float2(aspect, 1.0);

                float xRadius = length((xUv - centerUv) * aspectScale);
                float yRadius = length((yUv - centerUv) * aspectScale);
                float zRadius = length((zUv - centerUv) * aspectScale);

                return max(max(xRadius, yRadius), max(zRadius, 0.0001));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                float4 centerHCS = TransformObjectToHClip(float3(0.0, 0.0, 0.0));
                float2 centerUv = ScreenUvFromClip(centerHCS);
                float projectedRadius = ProjectedRadius(centerUv);
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 0.0001);

                output.positionHCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.centerAndRadius = float4(centerUv, projectedRadius, aspect);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.00001);
                float2 centerUv = input.centerAndRadius.xy;
                float projectedRadius = max(input.centerAndRadius.z, 0.0001);
                float aspect = input.centerAndRadius.w;

                float2 screenDelta = screenUv - centerUv;
                float2 aspectDelta = screenDelta * float2(aspect, 1.0);
                float normalizedRadius = length(aspectDelta) / projectedRadius;

                float outerMask = 1.0 - smoothstep(1.0 - _OuterFade, 1.0, normalizedRadius);
                float innerMask = smoothstep(0.08, 0.28, normalizedRadius);
                float lensMask = saturate(outerMask * innerMask);

                float2 screenDirection = screenDelta / max(length(screenDelta), 0.00001);
                float bendProximity = saturate(1.0 - normalizedRadius);
                float bendAmount = _BendStrength * projectedRadius * pow(bendProximity, _BendFalloff);
                bendAmount /= max(normalizedRadius, _BendClamp);

                float2 bentUv = saturate(screenUv + screenDirection * bendAmount * lensMask);
                half3 sceneColor = SampleSceneColor(bentUv);

                if (_ChromaticAberration > 0.0001)
                {
                    float chromaOffset = _ChromaticAberration * projectedRadius * lensMask * bendProximity;
                    sceneColor.r = SampleSceneColor(saturate(bentUv + screenDirection * chromaOffset)).r;
                    sceneColor.b = SampleSceneColor(saturate(bentUv - screenDirection * chromaOffset)).b;
                }

                half edgeGlow = pow(saturate(normalizedRadius), _LensGlowPower) * outerMask;
                half3 color = sceneColor + _LensGlowColor.rgb * edgeGlow * _LensGlowIntensity * _ExternalPulseIntensity;
                half alpha = saturate(lensMask * _LensOpacity + edgeGlow * 0.22h);

                clip(alpha - 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
