Shader "Starfall/3D/BlackHole/SingularitySimple"
{
    Properties
    {
        [Header(Shape)]
        _ObjectRadius("Mesh Local Radius", Float) = 0.5
        _EventHorizonRadius("Event Horizon Radius", Range(0.05, 0.95)) = 0.64
        _HorizonSoftness("Horizon Edge Softness", Range(0.001, 0.2)) = 0.018
        _OuterFade("Outer Fade", Range(0.001, 0.45)) = 0.12

        [Header(Event Horizon)]
        _EventHorizonColor("Event Horizon Color", Color) = (0.0, 0.0, 0.0, 1.0)

        [Header(Photon Ring)]
        [HDR]_PhotonRingColor("Photon Ring HDR Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _PhotonRingWidth("Photon Ring Width", Range(0.001, 0.25)) = 0.042
        _PhotonRingIntensity("Photon Ring Intensity", Range(0.0, 12.0)) = 2.8
        _PhotonRingAlpha("Photon Ring Alpha", Range(0.0, 1.0)) = 0.85

        [Header(Subtle Lensed Arc)]
        [HDR]_OffscreenDiskArcColor("Menace Arc HDR Color", Color) = (0.0, 1.06, 3.39, 1.0)
        _SubtleArcIntensity("Subtle Arc Intensity", Range(0.0, 8.0)) = 0.9
        _SubtleArcRadiusOffset("Subtle Arc Radius Offset", Range(-0.1, 0.35)) = 0.055
        _SubtleArcWidth("Subtle Arc Width", Range(0.001, 0.35)) = 0.075
        _SubtleArcVerticalBias("Subtle Arc Vertical Bias", Range(0.0, 6.0)) = 1.8
        _SubtleArcLineFrequency("Subtle Arc Line Frequency", Range(1.0, 100.0)) = 30.0
        _SubtleArcLineSharpness("Subtle Arc Line Sharpness", Range(0.25, 12.0)) = 3.2
        _SubtleArcSpinSpeed("Subtle Arc Spin Speed", Float) = 0.08

        [Header(Rear Disk Wrap)]
        _WrapArcIntensity("Wrap Arc Intensity", Range(0.0, 10.0)) = 2.0
        _WrapArcRadiusOffset("Wrap Arc Radius Offset", Range(-0.05, 0.4)) = 0.095
        _WrapArcWidth("Wrap Arc Width", Range(0.001, 0.4)) = 0.13
        _WrapArcUpperStrength("Wrap Arc Upper Strength", Range(0.0, 2.0)) = 1.0
        _WrapArcLowerStrength("Wrap Arc Lower Strength", Range(0.0, 2.0)) = 0.42
        _WrapArcLineFrequency("Wrap Arc Line Frequency", Range(1.0, 120.0)) = 34.0
        _WrapArcLineSharpness("Wrap Arc Line Sharpness", Range(0.25, 16.0)) = 4.5

        [Header(Screen Sample Wrap)]
        _ScreenWrapIntensity("Screen Wrap Intensity", Range(0.0, 8.0)) = 1.35
        _ScreenWrapRadialBend("Screen Wrap Radial Bend", Range(-0.5, 0.5)) = -0.08
        _ScreenWrapVerticalPull("Screen Wrap Vertical Pull", Range(0.0, 0.5)) = 0.11
        _ScreenWrapSourceThreshold("Screen Wrap Source Threshold", Range(0.0, 8.0)) = 0.45
        _ScreenWrapBoost("Screen Wrap Boost", Range(0.0, 4.0)) = 0.85

        [Header(Outer Halo)]
        [HDR]_HaloColor("Halo HDR Color", Color) = (0.0, 0.45, 1.25, 1.0)
        _HaloIntensity("Halo Intensity", Range(0.0, 6.0)) = 0.55
        _HaloAlpha("Halo Alpha", Range(0.0, 1.0)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SingularitySimple"
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

            #define STARFALL_TWO_PI 6.28318530718

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
                float _EventHorizonRadius;
                float _HorizonSoftness;
                float _OuterFade;

                half4 _EventHorizonColor;

                half4 _PhotonRingColor;
                float _PhotonRingWidth;
                half _PhotonRingIntensity;
                half _PhotonRingAlpha;

                half4 _OffscreenDiskArcColor;
                half _SubtleArcIntensity;
                float _SubtleArcRadiusOffset;
                float _SubtleArcWidth;
                float _SubtleArcVerticalBias;
                float _SubtleArcLineFrequency;
                float _SubtleArcLineSharpness;
                float _SubtleArcSpinSpeed;

                half _WrapArcIntensity;
                float _WrapArcRadiusOffset;
                float _WrapArcWidth;
                half _WrapArcUpperStrength;
                half _WrapArcLowerStrength;
                float _WrapArcLineFrequency;
                float _WrapArcLineSharpness;

                half _ScreenWrapIntensity;
                float _ScreenWrapRadialBend;
                float _ScreenWrapVerticalPull;
                half _ScreenWrapSourceThreshold;
                half _ScreenWrapBoost;

                half4 _HaloColor;
                half _HaloIntensity;
                half _HaloAlpha;
            CBUFFER_END

            half Luminance(half3 color)
            {
                return dot(color, half3(0.2126h, 0.7152h, 0.0722h));
            }

            float ScreenUvMask(float2 uv)
            {
                float2 edgeDistance = min(uv, 1.0 - uv);
                float edgeDistanceMin = min(edgeDistance.x, edgeDistance.y);
                float feather = 2.0 / max(min(_ScreenParams.x, _ScreenParams.y), 1.0);
                return smoothstep(0.0, feather, edgeDistanceMin);
            }

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
                float2 direction = aspectDelta / max(length(aspectDelta), 0.00001);

                float horizonRadius = saturate(_EventHorizonRadius);
                float outerMask = 1.0 - smoothstep(1.0 - _OuterFade, 1.0, normalizedRadius);
                float coreMask = 1.0 - smoothstep(horizonRadius, horizonRadius + _HorizonSoftness, normalizedRadius);

                if (coreMask > 0.999)
                {
                    return half4(_EventHorizonColor.rgb, 1.0h);
                }

                float photonDistance = abs(normalizedRadius - horizonRadius);
                float photonRing = 1.0 - smoothstep(0.0, max(_PhotonRingWidth, 0.0001), photonDistance);
                float photonCore = 1.0 - smoothstep(0.0, max(_PhotonRingWidth * 0.3, 0.0001), photonDistance);
                float photonBloom = 1.0 - smoothstep(0.0, max(_PhotonRingWidth * 2.25, 0.0001), photonDistance);
                photonRing *= outerMask;
                photonCore *= outerMask;
                photonBloom *= outerMask;

                float arcRadius = horizonRadius + _SubtleArcRadiusOffset;
                float arcMask = 1.0 - smoothstep(0.0, max(_SubtleArcWidth, 0.0001), abs(normalizedRadius - arcRadius));
                float verticalMask = pow(saturate(abs(direction.y)), _SubtleArcVerticalBias);
                float arcAngle01 = frac(atan2(direction.y, direction.x) / STARFALL_TWO_PI + 0.5);
                float laneWave = 0.5 + 0.5 * sin((normalizedRadius * _SubtleArcLineFrequency + arcAngle01 * 2.0 - _Time.y * _SubtleArcSpinSpeed) * STARFALL_TWO_PI);
                float laneMask = pow(saturate(laneWave), _SubtleArcLineSharpness);
                float arc = saturate(arcMask * verticalMask * laneMask * outerMask);

                float wrapRadius = horizonRadius + _WrapArcRadiusOffset;
                float wrapRadialMask = 1.0 - smoothstep(0.0, max(_WrapArcWidth, 0.0001), abs(normalizedRadius - wrapRadius));
                float wrapVerticalBand = pow(saturate(abs(direction.y)), 0.68);
                float upperWrap = smoothstep(0.02, 0.5, direction.y) * _WrapArcUpperStrength;
                float lowerWrap = smoothstep(0.12, 0.75, -direction.y) * _WrapArcLowerStrength;
                float wrapLaneWave = 0.5 + 0.5 * sin((normalizedRadius * _WrapArcLineFrequency + arcAngle01 * 3.0 - _Time.y * _SubtleArcSpinSpeed) * STARFALL_TWO_PI);
                float wrapLanes = pow(saturate(wrapLaneWave), _WrapArcLineSharpness);
                float wrapCore = pow(saturate(1.0 - abs(normalizedRadius - wrapRadius) / max(_WrapArcWidth, 0.0001)), 1.7);
                float wrapArc = saturate(wrapRadialMask * wrapVerticalBand * (upperWrap + lowerWrap) * (wrapLanes * 0.75 + wrapCore * 0.45) * outerMask);

                float2 screenDirection = screenDelta / max(length(screenDelta), 0.00001);
                float2 screenWrapUv = screenUv
                    + screenDirection * (_ScreenWrapRadialBend * projectedRadius)
                    - float2(0.0, sign(direction.y) * _ScreenWrapVerticalPull * projectedRadius);
                float screenWrapUvMask = ScreenUvMask(screenWrapUv);
                half3 screenWrapColor = SampleSceneColor(saturate(screenWrapUv));
                half screenWrapLuma = Luminance(screenWrapColor);
                float screenWrapSourceMask = smoothstep(
                    _ScreenWrapSourceThreshold,
                    _ScreenWrapSourceThreshold + max(_ScreenWrapSourceThreshold * 0.75h, 0.05h),
                    screenWrapLuma
                );
                float screenWrap = saturate(wrapRadialMask * wrapVerticalBand * (upperWrap + lowerWrap) * screenWrapSourceMask * screenWrapUvMask * outerMask);

                float haloFalloff = pow(saturate(1.0 - normalizedRadius), 2.35) * outerMask;

                half3 color = _HaloColor.rgb * haloFalloff * _HaloIntensity;
                color += _OffscreenDiskArcColor.rgb * arc * _SubtleArcIntensity;
                color += _OffscreenDiskArcColor.rgb * wrapArc * _WrapArcIntensity;
                color += screenWrapColor * screenWrap * _ScreenWrapIntensity * (1.0h + _ScreenWrapBoost);
                color += _PhotonRingColor.rgb * photonBloom * _PhotonRingIntensity * 0.32h;
                color += _PhotonRingColor.rgb * photonRing * _PhotonRingIntensity * 0.78h;
                color += half3(1.0h, 0.96h, 0.86h) * photonCore * _PhotonRingIntensity * 0.85h;

                color = lerp(color, _EventHorizonColor.rgb, saturate(coreMask));

                half alpha = saturate(max(coreMask, photonRing * _PhotonRingAlpha));
                alpha = saturate(max(alpha, photonBloom * _PhotonRingAlpha * 0.45h));
                alpha = saturate(max(alpha, arc * 0.55h));
                alpha = saturate(max(alpha, wrapArc * 0.72h));
                alpha = saturate(max(alpha, screenWrap * 0.85h));
                alpha = saturate(max(alpha, haloFalloff * _HaloAlpha));

                clip(alpha - 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
