Shader "Starfall/3D/BlackHole/SingularityLensing"
{
    Properties
    {
        [Header(Screen Space Lensing)]
        _ObjectRadius("Mesh Local Radius", Float) = 0.5
        _EventHorizonRadius("Event Horizon Radius", Range(0.05, 0.95)) = 0.56
        _HorizonSoftness("Horizon Edge Softness", Range(0.001, 0.25)) = 0.035
        _LensOpacity("Lens Opacity", Range(0.0, 1.0)) = 0.92
        _BendStrength("Bend Strength", Range(-1.5, 1.5)) = -0.65
        _BendFalloff("Bend Falloff", Range(0.25, 8.0)) = 1.35
        _BendClamp("Bend Clamp", Range(0.02, 0.6)) = 0.16
        _OuterFade("Outer Lens Fade", Range(0.001, 0.45)) = 0.18
        _ChromaticAberration("Chromatic Edge Split", Range(0.0, 0.08)) = 0.012
        _LensCausticBoost("Lensed Background Brightening", Range(0.0, 3.0)) = 0.45
        _LensedSourceThickness("Lensed Source Thickness", Range(0.0, 0.08)) = 0.012
        _LensedSourceThreshold("Lensed Source Threshold", Range(0.0, 8.0)) = 0.6
        _LensedSourceBoost("Lensed Source Boost", Range(0.0, 3.0)) = 0.35
        _LensedSourceRingWidth("Lensed Source Ring Width", Range(0.001, 0.5)) = 0.18

        [Header(Lensed Disk Arc)]
        _DiskArcIntensity("Disk Arc Intensity", Range(0.0, 12.0)) = 3.0
        _DiskArcRadiusOffset("Disk Arc Radius Offset", Range(-0.25, 0.45)) = 0.11
        _DiskArcWidth("Disk Arc Width", Range(0.001, 0.5)) = 0.16
        _DiskArcVerticalBias("Disk Arc Vertical Bias", Range(0.0, 6.0)) = 1.2
        _DiskArcLineFrequency("Disk Arc Line Frequency", Range(4.0, 120.0)) = 44.0
        _DiskArcLineSharpness("Disk Arc Line Sharpness", Range(0.25, 16.0)) = 6.0
        _DiskArcNoiseScale("Disk Arc Noise Scale", Range(0.5, 40.0)) = 12.0
        _DiskArcNoiseStrength("Disk Arc Noise Strength", Range(0.0, 2.0)) = 0.35
        _DiskArcCoreStrength("Disk Arc Core Strength", Range(0.0, 4.0)) = 1.1
        _DiskArcSpinSpeed("Disk Arc Spin Speed", Float) = 0.24
        _DiskArcInfallSpeed("Disk Arc Infall Speed", Float) = 0.18
        _DiskArcSpiralTightness("Disk Arc Spiral Tightness", Range(0.0, 8.0)) = 2.4
        _DiskArcDopplerBoost("Disk Arc Side Boost", Range(0.0, 3.0)) = 1.0
        _DiskArcFarSideDimming("Disk Arc Far-Side Dimming", Range(0.0, 0.95)) = 0.18
        _DiskArcHotSideAngle("Disk Arc Hot Side Angle", Range(-3.1416, 3.1416)) = -0.65

        [Header(Photon Ring)]
        [HDR]_PhotonRingColor("Photon Ring HDR Color", Color) = (6.0, 2.05, 0.42, 1.0)
        _PhotonRingWidth("Photon Ring Width", Range(0.001, 0.25)) = 0.045
        _PhotonRingIntensity("Photon Ring Intensity", Range(0.0, 12.0)) = 2.6
        _PhotonRingAlpha("Photon Ring Alpha", Range(0.0, 1.0)) = 0.68

        [Header(Event Horizon)]
        _EventHorizonColor("Event Horizon Color", Color) = (0.0, 0.0, 0.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SingularityLensing"
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
                float _EventHorizonRadius;
                float _HorizonSoftness;
                float _LensOpacity;
                float _BendStrength;
                float _BendFalloff;
                float _BendClamp;
                float _OuterFade;
                float _ChromaticAberration;
                half _LensCausticBoost;
                float _LensedSourceThickness;
                half _LensedSourceThreshold;
                half _LensedSourceBoost;
                float _LensedSourceRingWidth;

                half _DiskArcIntensity;
                float _DiskArcRadiusOffset;
                float _DiskArcWidth;
                float _DiskArcVerticalBias;
                float _DiskArcLineFrequency;
                float _DiskArcLineSharpness;
                float _DiskArcNoiseScale;
                float _DiskArcNoiseStrength;
                half _DiskArcCoreStrength;
                float _DiskArcSpinSpeed;
                float _DiskArcInfallSpeed;
                float _DiskArcSpiralTightness;
                half _DiskArcDopplerBoost;
                half _DiskArcFarSideDimming;
                float _DiskArcHotSideAngle;

                half4 _PhotonRingColor;
                float _PhotonRingWidth;
                half _PhotonRingIntensity;
                half _PhotonRingAlpha;

                half4 _EventHorizonColor;
            CBUFFER_END

            #define STARFALL_TWO_PI 6.28318530718

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2d(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                value += noise2d(p) * amplitude;
                p *= 2.03;
                amplitude *= 0.5;

                value += noise2d(p) * amplitude;
                p *= 2.01;
                amplitude *= 0.5;

                value += noise2d(p) * amplitude;

                return value;
            }

            float Luminance(half3 color)
            {
                return dot(color, half3(0.2126h, 0.7152h, 0.0722h));
            }

            half3 KeepBrighter(half3 current, half3 candidate)
            {
                float currentLuma = Luminance(current);
                float candidateLuma = Luminance(candidate);
                return lerp(current, candidate, step(currentLuma, candidateLuma));
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

                float horizonRadius = saturate(_EventHorizonRadius);
                float horizonMask = 1.0 - smoothstep(horizonRadius, horizonRadius + _HorizonSoftness, normalizedRadius);

                float outerMask = 1.0 - smoothstep(1.0 - _OuterFade, 1.0, normalizedRadius);
                float lensInnerMask = smoothstep(
                    max(horizonRadius - _HorizonSoftness, 0.0),
                    horizonRadius + _PhotonRingWidth,
                    normalizedRadius
                );
                float lensMask = saturate(lensInnerMask * outerMask);

                float2 screenDirection = screenDelta / max(length(screenDelta), 0.00001);
                float bendRange = max(1.0 - horizonRadius, 0.0001);
                float bendProximity = saturate((1.0 - normalizedRadius) / bendRange);
                float bendAmount = _BendStrength * projectedRadius * pow(bendProximity, _BendFalloff);
                bendAmount /= max(normalizedRadius, _BendClamp);

                float2 bentUv = saturate(screenUv + screenDirection * bendAmount * lensMask);
                half3 sceneColor = SampleSceneColor(bentUv);

                if (_ChromaticAberration > 0.0001)
                {
                    float chromaOffset = _ChromaticAberration * projectedRadius * lensMask * bendProximity;
                    float2 redUv = saturate(bentUv + screenDirection * chromaOffset);
                    float2 blueUv = saturate(bentUv - screenDirection * chromaOffset);

                    sceneColor.r = SampleSceneColor(redUv).r;
                    sceneColor.b = SampleSceneColor(blueUv).b;
                }

                float sourceThickness = _LensedSourceThickness * projectedRadius * lensMask;
                if (sourceThickness > 0.00001)
                {
                    float2 radialOffset = screenDirection * sourceThickness;
                    float2 tangentOffset = float2(-screenDirection.y, screenDirection.x) * sourceThickness;

                    half3 gatheredColor = sceneColor;
                    gatheredColor = KeepBrighter(gatheredColor, SampleSceneColor(saturate(bentUv + radialOffset)));
                    gatheredColor = KeepBrighter(gatheredColor, SampleSceneColor(saturate(bentUv - radialOffset)));
                    gatheredColor = KeepBrighter(gatheredColor, SampleSceneColor(saturate(bentUv + tangentOffset)));
                    gatheredColor = KeepBrighter(gatheredColor, SampleSceneColor(saturate(bentUv - tangentOffset)));

                    float gatheredLuma = Luminance(gatheredColor);
                    float sourceMask = smoothstep(
                        _LensedSourceThreshold,
                        _LensedSourceThreshold + max(_LensedSourceThreshold * 0.75h, 0.05h),
                        gatheredLuma
                    );
                    float ringMask = 1.0 - smoothstep(0.0, max(_LensedSourceRingWidth, 0.0001), abs(normalizedRadius - horizonRadius));
                    float gatherMask = saturate(sourceMask * ringMask * lensMask);
                    sceneColor = lerp(sceneColor, gatheredColor * (1.0h + _LensedSourceBoost), gatherMask);
                }

                float caustic = pow(saturate(bendProximity), 1.65) * lensMask;
                sceneColor *= 1.0 + caustic * _LensCausticBoost;

                float photonDistance = abs(normalizedRadius - horizonRadius);
                float photonRing = 1.0 - smoothstep(0.0, max(_PhotonRingWidth, 0.0001), photonDistance);
                float photonCore = 1.0 - smoothstep(0.0, max(_PhotonRingWidth * 0.28, 0.0001), photonDistance);
                float photonBloom = 1.0 - smoothstep(0.0, max(_PhotonRingWidth * 1.85, 0.0001), photonDistance);
                photonRing *= outerMask;
                photonCore *= outerMask;
                photonBloom *= outerMask;

                half3 color = sceneColor;

                float arcRadius = horizonRadius + _DiskArcRadiusOffset;
                float diskArc = 1.0 - smoothstep(0.0, max(_DiskArcWidth, 0.0001), abs(normalizedRadius - arcRadius));
                float verticalArcMask = pow(saturate(abs(screenDirection.y)), _DiskArcVerticalBias);
                diskArc *= verticalArcMask * outerMask;

                float2 lensDirection = aspectDelta / max(length(aspectDelta), 0.00001);
                float arcAngle01 = frac(atan2(lensDirection.y, lensDirection.x) / STARFALL_TWO_PI + 0.5);
                float arcRadial01 = saturate((normalizedRadius - horizonRadius) / max(_DiskArcWidth, 0.0001));
                float time = _Time.y;

                float spiralCoord = arcAngle01 + arcRadial01 * _DiskArcSpiralTightness - time * _DiskArcSpinSpeed;
                float radialFlow = arcRadial01 - time * _DiskArcInfallSpeed;

                float arcNoise = fbm(float2(
                    spiralCoord * _DiskArcNoiseScale * 1.75,
                    radialFlow * _DiskArcNoiseScale
                ));

                float wrappedLaneWave = 0.5 + 0.5 * sin(
                    (
                        normalizedRadius * _DiskArcLineFrequency +
                        arcNoise * _DiskArcNoiseStrength -
                        time * _DiskArcInfallSpeed * 5.0
                    ) * STARFALL_TWO_PI
                );
                float wrappedLines = pow(saturate(wrappedLaneWave), _DiskArcLineSharpness);

                float strandNoise = fbm(float2(
                    spiralCoord * _DiskArcNoiseScale * 5.0,
                    radialFlow * _DiskArcNoiseScale * 0.65
                ));
                float strandMask = smoothstep(0.42, 0.95, strandNoise);

                float brightCore = pow(saturate(1.0 - arcRadial01), 1.6) * _DiskArcCoreStrength;
                float lensedDiskPattern = saturate(wrappedLines * 1.2 + strandMask * 0.35 + brightCore);

                float arcHotSide01 = saturate(0.5 + 0.5 * cos((arcAngle01 - 0.5) * STARFALL_TWO_PI - _DiskArcHotSideAngle));
                float arcHotSide = pow(arcHotSide01, 2.7);
                float arcFarSide = pow(saturate(1.0 - arcHotSide01), 1.25);
                float arcRelativistic = max(0.08, 1.0 + arcHotSide * _DiskArcDopplerBoost - arcFarSide * _DiskArcFarSideDimming);

                half3 lineColor = lerp(_PhotonRingColor.rgb, half3(1.0h, 0.88h, 0.58h), saturate(wrappedLines + brightCore));
                color += lineColor * diskArc * lensedDiskPattern * _DiskArcIntensity * arcRelativistic;

                half3 photonWhite = half3(1.0h, 0.92h, 0.72h);
                color += _PhotonRingColor.rgb * photonBloom * _PhotonRingIntensity * 0.38h;
                color += _PhotonRingColor.rgb * photonRing * _PhotonRingIntensity * 0.72h;
                color += photonWhite * photonCore * _PhotonRingIntensity * 1.15h;
                color = lerp(color, _EventHorizonColor.rgb, saturate(horizonMask));

                half alpha = saturate(max(horizonMask, lensMask * _LensOpacity));
                alpha = saturate(max(alpha, max(photonRing, photonBloom * 0.45) * _PhotonRingAlpha));

                clip(alpha - 0.001);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
