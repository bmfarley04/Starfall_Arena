Shader "Starfall/3D/Pulsar/Jet"
{
    Properties
    {
        [Header(Color And Brightness)]
        [HDR]_JetColor("Jet HDR Color", Color) = (0.45, 3.0, 10.0, 1.0)
        [HDR]_HotCoreColor("Hot Core HDR Color", Color) = (8.0, 9.0, 10.0, 1.0)
        _Brightness("Brightness", Range(0.0, 30.0)) = 5.5
        _Alpha("Alpha", Range(0.0, 2.0)) = 0.7

        [Header(Flow)]
        _NoiseTex("Optional Seamless Noise Texture", 2D) = "white" {}
        _TextureInfluence("Texture Influence", Range(0.0, 1.0)) = 0.35
        _NoiseScale("Noise Scale", Range(0.5, 48.0)) = 9.0
        _NoiseStrength("Noise Strength", Range(0.0, 3.0)) = 1.25
        _ScrollSpeed("Outward Scroll Speed", Float) = 1.8
        _OutwardSign("Outward Sign", Float) = 1.0

        [Header(Soft Shape)]
        _FresnelPower("Fresnel Edge Power", Range(0.25, 12.0)) = 2.25
        _FresnelStrength("Fresnel Strength", Range(0.0, 8.0)) = 2.5
        _InnerFill("Inner Fill", Range(0.0, 1.0)) = 0.12
        _LengthFadePower("Length Fade Power", Range(0.1, 8.0)) = 0.7
        _BaseFadeDistance("Base Fade Distance", Range(0.0, 0.35)) = 0.03
        _TipFadeDistance("Tip Fade Distance", Range(0.0, 0.5)) = 0.18

        [Header(Unstable Surface)]
        _VertexJitterStrength("Vertex Jitter Strength", Range(0.0, 2.0)) = 0.16
        _JitterScale("Jitter Scale", Range(0.5, 32.0)) = 6.0
        _JitterSpeed("Jitter Speed", Float) = 1.2

        [Header(Pulse)]
        _PulseStrength("Pulse Strength", Range(0.0, 3.0)) = 0.28
        _PulseSpeed("Pulse Speed", Float) = 0.7
        _ExternalPulseIntensity("External Pulse Intensity", Range(0.0, 4.0)) = 1.0
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
            Name "PulsarJet"
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

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

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
                float outward01 : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _JetColor;
                half4 _HotCoreColor;
                half _Brightness;
                half _Alpha;

                float4 _NoiseTex_ST;
                half _TextureInfluence;
                float _NoiseScale;
                half _NoiseStrength;
                float _ScrollSpeed;
                float _OutwardSign;

                half _FresnelPower;
                half _FresnelStrength;
                half _InnerFill;
                half _LengthFadePower;
                half _BaseFadeDistance;
                half _TipFadeDistance;

                half _VertexJitterStrength;
                float _JitterScale;
                float _JitterSpeed;

                half _PulseStrength;
                float _PulseSpeed;
                half _ExternalPulseIntensity;
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
                p = p * 2.02 + 13.1;
                amplitude *= 0.5;

                value += noise2d(p) * amplitude;
                p = p * 2.03 + 27.7;
                amplitude *= 0.5;

                value += noise2d(p) * amplitude;
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float outward01 = saturate(input.positionOS.y * _OutwardSign * 0.5 + 0.5);
                float time = _Time.y;
                float jitterNoise = fbm(float2(
                    input.uv.x * _JitterScale + time * 0.25,
                    outward01 * _JitterScale - time * _JitterSpeed
                ));
                float waveJitter = sin((outward01 * 8.0 + input.uv.x * 3.0 - time * _JitterSpeed) * STARFALL_TWO_PI);
                float jitter = ((jitterNoise - 0.5) + waveJitter * 0.25) * _VertexJitterStrength;

                float baseMask = smoothstep(0.0, 0.08, outward01);
                float tipMask = 1.0 - smoothstep(0.82, 1.0, outward01);
                float3 positionOS = input.positionOS.xyz + input.normalOS * jitter * baseMask * tipMask;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = input.uv;
                output.outward01 = outward01;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float outward01 = saturate(input.outward01);
                float2 flowUv = float2(input.uv.x, outward01);
                flowUv = flowUv * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                flowUv.y -= time * _ScrollSpeed;

                half textureNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowUv * _NoiseScale).r;
                float proceduralNoise = fbm(float2(input.uv.x * _NoiseScale * 1.7, outward01 * _NoiseScale - time * _ScrollSpeed));
                float fineNoise = fbm(float2(input.uv.x * _NoiseScale * 5.1 + 9.7, outward01 * _NoiseScale * 0.45 - time * _ScrollSpeed * 1.55));
                half energyNoise = saturate((proceduralNoise * 0.7 + fineNoise * 0.45) * _NoiseStrength);
                energyNoise = lerp(energyNoise, saturate(energyNoise * textureNoise * 1.55h), _TextureInfluence);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(saturate(1.0h - dot(normalWS, viewDirectionWS)), _FresnelPower) * _FresnelStrength;
                half shellMask = saturate(fresnel + _InnerFill);

                half baseFade = _BaseFadeDistance <= 0.0001h ? 1.0h : smoothstep(0.0h, _BaseFadeDistance, outward01);
                half tipFadeStart = 1.0h - _TipFadeDistance;
                half tipFade = _TipFadeDistance <= 0.0001h ? 1.0h : 1.0h - smoothstep(tipFadeStart, 1.0h, outward01);
                half lengthFade = pow(saturate(baseFade * tipFade), _LengthFadePower);

                half pulse = 1.0h + sin(time * _PulseSpeed * STARFALL_TWO_PI) * _PulseStrength * _ExternalPulseIntensity;
                pulse = max(pulse, 0.0h);

                half alpha = saturate(_Alpha * shellMask * lengthFade * energyNoise * pulse);
                half hotCore = saturate(energyNoise * 0.65h + fresnel * 0.35h);
                half3 color = lerp(_JetColor.rgb, _HotCoreColor.rgb, hotCore);
                color *= _Brightness * pulse * saturate(0.45h + energyNoise);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
