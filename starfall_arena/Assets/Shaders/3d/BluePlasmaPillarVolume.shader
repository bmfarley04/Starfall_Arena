Shader "Starfall/3D/BluePlasmaPillarVolume"
{
    Properties
    {
        [HDR]_CoreColor("Core HDR Color", Color) = (4.8, 6.2, 8.0, 1.0)
        [HDR]_ShellColor("Cloud Shell HDR Color", Color) = (1.25, 2.3, 4.5, 1.0)
        [HDR]_RimColor("Rim HDR Color", Color) = (6.5, 8.0, 9.5, 1.0)
        _Brightness("Global Brightness", Range(0.0, 20.0)) = 2.8
        _Opacity("Opacity", Range(0.0, 2.0)) = 1.0
        _Reveal("Reveal", Range(0.0, 1.0)) = 1.0
        [HideInInspector]_LayerMode("Layer Mode", Range(0.0, 2.0)) = 1.0
        [HideInInspector]_LayerIntensity("Layer Intensity", Range(0.0, 6.0)) = 1.0

        _FresnelPower("Fresnel Power", Range(0.25, 12.0)) = 2.1
        _FresnelStrength("Fresnel Strength", Range(0.0, 10.0)) = 3.4
        _CloudScale("Cloud Scale", Range(0.1, 50.0)) = 9.5
        _CloudSpeed("Cloud Speed", Float) = 0.58
        _CloudContrast("Cloud Contrast", Range(0.25, 8.0)) = 3.0
        _DarkPocketThreshold("Dark Pocket Threshold", Range(0.0, 1.0)) = 0.48
        _DarkPocketStrength("Dark Pocket Strength", Range(0.0, 1.0)) = 0.58
        _VerticalScale("Vertical Scale", Range(0.01, 4.0)) = 0.18
        _TwistStrength("Twist Strength", Range(-4.0, 4.0)) = 0.85
        _PulseSpeed("Pulse Speed", Float) = 1.15
        _PulseStrength("Pulse Strength", Range(0.0, 2.0)) = 0.12
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
            Name "BluePlasmaPillarVolume"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TWO_PI 6.28318530718

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
                float3 positionOS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _ShellColor;
                half4 _RimColor;
                half _Brightness;
                half _Opacity;
                half _Reveal;
                half _LayerMode;
                half _LayerIntensity;
                half _FresnelPower;
                half _FresnelStrength;
                float _CloudScale;
                float _CloudSpeed;
                half _CloudContrast;
                half _DarkPocketThreshold;
                half _DarkPocketStrength;
                float _VerticalScale;
                half _TwistStrength;
                float _PulseSpeed;
                half _PulseStrength;
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
                float amp = 0.5;
                value += noise2d(p) * amp;
                p = p * 2.03 + 13.7;
                amp *= 0.5;
                value += noise2d(p) * amp;
                p = p * 2.11 + 31.1;
                amp *= 0.5;
                value += noise2d(p) * amp;
                p = p * 2.07 + 7.9;
                amp *= 0.5;
                value += noise2d(p) * amp;
                return saturate(value);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDir)), _FresnelPower) * _FresnelStrength;

                float time = _Time.y;
                float vertical = input.positionWS.y * _VerticalScale;
                float angle01 = frac(atan2(input.positionOS.z, input.positionOS.x) / TWO_PI + 0.5 + vertical * _TwistStrength * 0.03);
                float2 cloudUv = float2(angle01 * _CloudScale + time * 0.05, vertical - time * _CloudSpeed);
                float cloudA = fbm(cloudUv);
                float cloudB = fbm(cloudUv * float2(0.58, 1.85) + float2(17.4, time * 0.16));
                float cloud = saturate((cloudA * 0.68 + cloudB * 0.32) * _CloudContrast);
                float darkPocket = smoothstep(_DarkPocketThreshold, 1.0, cloud);
                float preservedDepth = lerp(1.0 - _DarkPocketStrength, 1.0, darkPocket);
                float endFade = smoothstep(0.0, 0.035, input.uv.y) * (1.0 - smoothstep(0.965, 1.0, input.uv.y));
                float pulse = 1.0 + sin(time * _PulseSpeed * TWO_PI) * _PulseStrength;

                half3 color;
                half alpha;
                if (_LayerMode < 0.5h)
                {
                    color = _CoreColor.rgb * lerp(0.72, 1.08, cloud);
                    alpha = saturate((0.36 + cloud * 0.24) * _Opacity * _Reveal * endFade);
                }
                else if (_LayerMode < 1.5h)
                {
                    color = lerp(_ShellColor.rgb, _RimColor.rgb, saturate(cloud * 0.42 + fresnel * 0.18));
                    color *= preservedDepth;
                    alpha = saturate((0.08 + cloud * 0.22 + fresnel * 0.2) * _Opacity * _Reveal * endFade);
                }
                else
                {
                    color = _RimColor.rgb;
                    alpha = saturate((fresnel * 0.45 + cloud * 0.05) * _Opacity * _Reveal * endFade);
                }

                color *= _Brightness * _LayerIntensity * pulse * _Reveal;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
