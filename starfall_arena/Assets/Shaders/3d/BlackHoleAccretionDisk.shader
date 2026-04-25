Shader "Starfall/3D/BlackHole/AccretionDisk"
{
    Properties
    {
        [Header(Disk Shape)]
        _InnerRadius("Inner Radius", Range(0.0, 1.0)) = 0.18
        _OuterRadius("Outer Radius", Range(0.0, 1.0)) = 0.95
        _InnerFade("Inner Fade Width", Range(0.001, 1.0)) = 0.08
        _OuterFade("Outer Fade Width", Range(0.001, 1.5)) = 0.18

        [Header(Motion)]
        _SpinSpeed("Spin Speed", Float) = 0.24
        _InfallSpeed("Infall Speed", Float) = 0.18
        _SpiralTightness("Spiral Tightness", Range(0.0, 8.0)) = 2.4

        [Header(Texture Detail)]
        _NoiseScale("Noise Scale", Range(0.5, 40.0)) = 10.0
        _NoiseStrength("Noise Strength", Range(0.0, 2.0)) = 0.75
        _RingFrequency("Ring Frequency", Range(1.0, 80.0)) = 34.0
        _RingSharpness("Ring Sharpness", Range(0.25, 12.0)) = 3.0
        _WispSharpness("Wisp Sharpness", Range(0.25, 12.0)) = 2.4

        [Header(Color And Brightness)]
        [HDR]_InnerColor("Inner HDR Color", Color) = (5.0, 2.35, 0.55, 1.0)
        [HDR]_MidColor("Mid HDR Color", Color) = (2.6, 0.82, 0.16, 1.0)
        [HDR]_OuterColor("Outer HDR Color", Color) = (0.6, 0.16, 0.05, 1.0)
        [HDR]_HotStreakColor("Hot Streak HDR Color", Color) = (8.0, 6.2, 3.1, 1.0)
        _Brightness("Brightness", Range(0.0, 20.0)) = 3.6
        _Opacity("Opacity", Range(0.0, 2.0)) = 0.92
        _DepthClipThreshold("Depth Clip Threshold", Range(0.0, 0.25)) = 0.02
        _DopplerBoost("One-Side Hot Boost", Range(0.0, 3.0)) = 0.55
        _HotSideAngle("Hot Side Angle", Range(-3.1416, 3.1416)) = -0.65
    }

    SubShader
    {
        Tags
        {
            // This intentionally renders in the opaque range so URP copies it into
            // _CameraOpaqueTexture for the singularity lensing shader. ZWrite must
            // stay on here, otherwise the skybox draws afterward and erases the disk
            // anywhere there is no pre-existing opaque depth behind it.
            "Queue" = "Geometry+450"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "AccretionDisk"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define STARFALL_PI 3.14159265359
            #define STARFALL_TWO_PI 6.28318530718

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _InnerRadius;
                float _OuterRadius;
                float _InnerFade;
                float _OuterFade;

                float _SpinSpeed;
                float _InfallSpeed;
                float _SpiralTightness;

                float _NoiseScale;
                float _NoiseStrength;
                float _RingFrequency;
                float _RingSharpness;
                float _WispSharpness;

                half4 _InnerColor;
                half4 _MidColor;
                half4 _OuterColor;
                half4 _HotStreakColor;
                half _Brightness;
                half _Opacity;
                half _DepthClipThreshold;
                half _DopplerBoost;
                float _HotSideAngle;
            CBUFFER_END

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Quad-friendly polar coordinates: UV center is the black hole, corners are masked away.
                float2 diskPosition = input.uv * 2.0 - 1.0;
                float radius = length(diskPosition);

                float outerRadius = max(_OuterRadius, _InnerRadius + 0.001);
                float radial01 = saturate((radius - _InnerRadius) / (outerRadius - _InnerRadius));

                float innerMask = smoothstep(_InnerRadius, _InnerRadius + _InnerFade, radius);
                float outerMask = 1.0 - smoothstep(outerRadius - _OuterFade, outerRadius, radius);
                float diskMask = saturate(innerMask * outerMask);

                float angle = atan2(diskPosition.y, diskPosition.x);
                float angular01 = frac(angle / STARFALL_TWO_PI + 0.5);
                float time = _Time.y;

                float spiralCoord = angular01 + radial01 * _SpiralTightness - time * _SpinSpeed;
                float radialFlow = radial01 - time * _InfallSpeed;

                float broadNoise = fbm(float2(spiralCoord * _NoiseScale, radialFlow * _NoiseScale * 0.55));
                float fineNoise = fbm(float2(spiralCoord * _NoiseScale * 2.4 + 17.0, radialFlow * _NoiseScale * 1.7));
                float noiseValue = saturate((broadNoise * 0.72 + fineNoise * 0.44) * _NoiseStrength);

                float ringWave = 0.5 + 0.5 * sin((radial01 * _RingFrequency + broadNoise * 2.0 - time * _InfallSpeed * 5.0) * STARFALL_TWO_PI);
                float ringLines = pow(saturate(ringWave), _RingSharpness);
                float wisps = pow(saturate(noiseValue), _WispSharpness);

                float hotSide = 0.5 + 0.5 * cos(angle - _HotSideAngle);
                hotSide = pow(saturate(hotSide), 2.6) * _DopplerBoost;

                half3 gradient = lerp(_InnerColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.55, radial01));
                gradient = lerp(gradient, _OuterColor.rgb, smoothstep(0.45, 1.0, radial01));
                gradient = lerp(gradient, _HotStreakColor.rgb, saturate(hotSide * (0.35 + ringLines)));

                float laneIntensity = saturate(wisps * 0.85 + ringLines * 0.9);
                float innerHeat = 1.0 - smoothstep(0.0, 0.62, radial01);
                float alpha = diskMask * _Opacity * saturate(laneIntensity + innerHeat * 0.4);

                clip(alpha - _DepthClipThreshold);

                half3 color = gradient * _Brightness;
                color *= 0.35 + laneIntensity * 1.4 + innerHeat * 0.75 + hotSide;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
