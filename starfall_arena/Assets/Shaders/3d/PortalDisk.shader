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
        _RimSpinSpeed("Rim Spin Speed", Float) = 0.35
        _RimSegmentFrequency("Rim Segment Frequency", Range(1.0, 64.0)) = 18.0
        _RimSegmentContrast("Rim Segment Contrast", Range(0.0, 1.0)) = 0.35
        _RimSegmentSharpness("Rim Segment Sharpness", Range(0.25, 8.0)) = 2.4

        [Header(Inner Surface)]
        [HDR]_InnerColor("Inner HDR Color", Color) = (0.55, 0.1, 1.1, 1.0)
        _InnerBrightness("Inner Brightness", Range(0.0, 10.0)) = 0.75
        _CenterDarkness("Center Darkness", Range(0.0, 1.0)) = 0.48
        _InnerRadius("Inner Visible Radius", Range(0.0, 1.0)) = 0.82
        _InnerEdgeSoftness("Inner Edge Softness", Range(0.001, 0.5)) = 0.2
        _InnerGradientPower("Inner Gradient Power", Range(0.25, 8.0)) = 2.2
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

            Blend SrcAlpha OneMinusSrcAlpha
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
                half _RimSpinSpeed;
                half _RimSegmentFrequency;
                half _RimSegmentContrast;
                half _RimSegmentSharpness;

                half4 _InnerColor;
                half _InnerBrightness;
                half _CenterDarkness;
                half _InnerRadius;
                half _InnerEdgeSoftness;
                half _InnerGradientPower;
                half _Opacity;
            CBUFFER_END

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
                    float movingAngle = frac(angular01 + _Time.y * _RimSpinSpeed);
                    half rimSegments = 0.5h + 0.5h * sin(movingAngle * _RimSegmentFrequency * STARFALL_TWO_PI);
                    rimSegments = pow(saturate(rimSegments), _RimSegmentSharpness);
                    half spinModulation = lerp(1.0h - _RimSegmentContrast, 1.0h + _RimSegmentContrast, rimSegments);
                    half rimEnergy = saturate(rimMask * (1.0h + fresnel * _FresnelStrength) * spinModulation);

                    half alpha = saturate(rimEnergy);
                    half3 color = _RimColor.rgb * _RimBrightness * rimEnergy;
                    return half4(color, alpha);
                }

                float innerMask = 1.0 - smoothstep(_InnerRadius - _InnerEdgeSoftness, _InnerRadius, radius);
                innerMask *= diskMask;

                float radial01 = saturate(radius / max(_InnerRadius, 0.001));
                half edgeGradient = pow(saturate(radial01), _InnerGradientPower);
                half centerDim = saturate(1.0h - _CenterDarkness);
                half surfaceEnergy = lerp(centerDim, 1.0h, edgeGradient);
                half3 color = _InnerColor.rgb * _InnerBrightness * surfaceEnergy;

                half alpha = saturate(innerMask * _Opacity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
