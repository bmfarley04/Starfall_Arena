Shader "Starfall/3D/PortalDisk"
{
    Properties
    {
        [Header(Layer)]
        [Enum(InnerSurface,0,OuterRim,1)] _LayerMode("Layer Mode", Float) = 0

        [Header(Rim)]
        [HDR]_RimColor("Rim HDR Color", Color) = (5.8, 2.7, 8.5, 1.0)
        _RimBrightness("Rim Brightness", Range(0.0, 20.0)) = 4.4
        _RimWidth("Rim Width", Range(0.01, 0.5)) = 0.105
        _RimSoftness("Rim Softness", Range(0.001, 0.5)) = 0.09
        _FresnelStrength("Fresnel Strength", Range(0.0, 8.0)) = 1.15
        _FresnelPower("Fresnel Power", Range(0.25, 12.0)) = 3.0

        [Header(Inner Surface)]
        [HDR]_InnerColor("Inner HDR Color", Color) = (0.55, 0.1, 1.1, 1.0)
        [HDR]_InnerHotColor("Inner Hot Streak HDR Color", Color) = (2.2, 0.7, 4.0, 1.0)
        _InnerBrightness("Inner Brightness", Range(0.0, 10.0)) = 0.75
        _CenterDarkness("Center Darkness", Range(0.0, 1.0)) = 0.48
        _InnerRadius("Inner Visible Radius", Range(0.0, 1.0)) = 0.82
        _InnerEdgeSoftness("Inner Edge Softness", Range(0.001, 0.5)) = 0.2
        _SwirlSpeed("Swirl Speed", Float) = 0.045
        _SwirlStrength("Swirl Strength", Range(0.0, 4.0)) = 0.65
        _NoiseScale("Noise Scale", Range(0.5, 32.0)) = 6.5
        _Opacity("Opacity", Range(0.0, 2.0)) = 0.82
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PortalDisk"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define STARFALL_TWO_PI 6.28318530718

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float _LayerMode;

                half4 _RimColor;
                half _RimBrightness;
                half _RimWidth;
                half _RimSoftness;
                half _FresnelStrength;
                half _FresnelPower;

                half4 _InnerColor;
                half4 _InnerHotColor;
                half _InnerBrightness;
                half _CenterDarkness;
                half _InnerRadius;
                half _InnerEdgeSoftness;
                half _SwirlSpeed;
                half _SwirlStrength;
                half _NoiseScale;
                half _Opacity;
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
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radius = length(centeredUv);
                float angle = atan2(centeredUv.y, centeredUv.x);
                float angular01 = frac(angle / STARFALL_TWO_PI + 0.5);
                float time = _Time.y;

                float diskMask = 1.0 - smoothstep(0.985, 1.0, radius);

                if (_LayerMode > 0.5)
                {
                    float rimStart = saturate(1.0 - _RimWidth);
                    float rimOuter = 1.0 - smoothstep(1.0 - _RimSoftness, 1.0, radius);
                    float rimInner = smoothstep(rimStart - _RimSoftness, rimStart + _RimSoftness, radius);
                    float rimMask = saturate(rimInner * rimOuter);

                    float3 normalWS = normalize(input.normalWS);
                    float3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                    half fresnel = pow(saturate(1.0h - abs(dot(normalWS, viewDirectionWS))), _FresnelPower);
                    half rimEnergy = saturate(rimMask * (1.0h + fresnel * _FresnelStrength));

                    half alpha = saturate(rimEnergy);
                    half3 color = _RimColor.rgb * _RimBrightness * rimEnergy;
                    return half4(color, alpha);
                }

                float innerMask = 1.0 - smoothstep(_InnerRadius - _InnerEdgeSoftness, _InnerRadius, radius);
                innerMask *= diskMask;

                float radial01 = saturate(radius / max(_InnerRadius, 0.001));
                float swirlCoord = angular01 + radial01 * _SwirlStrength - time * _SwirlSpeed;
                float radialFlow = radial01 - time * _SwirlSpeed * 0.45;
                float broadNoise = fbm(float2(swirlCoord * _NoiseScale, radialFlow * _NoiseScale * 0.65));
                float fineNoise = fbm(float2(swirlCoord * _NoiseScale * 2.0 + 19.0, radialFlow * _NoiseScale * 1.45));
                float noiseValue = saturate(broadNoise * 0.72 + fineNoise * 0.35);

                float spiralBand = 0.5 + 0.5 * sin((swirlCoord * 2.0 + radial01 * 3.5 + noiseValue * 0.8) * STARFALL_TWO_PI);
                spiralBand = pow(saturate(spiralBand), 3.2);

                float centerDim = lerp(1.0 - _CenterDarkness, 1.0, smoothstep(0.0, 0.7, radial01));
                half surfaceEnergy = saturate(0.18h + noiseValue * 0.38h + spiralBand * 0.42h) * centerDim;
                half3 color = lerp(_InnerColor.rgb, _InnerHotColor.rgb, saturate(spiralBand * 0.55h + noiseValue * 0.25h));
                color *= _InnerBrightness * surfaceEnergy;

                half alpha = saturate(innerMask * _Opacity * (0.24h + surfaceEnergy));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
