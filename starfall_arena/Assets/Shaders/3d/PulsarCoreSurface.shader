Shader "Starfall/3D/Pulsar/CoreSurface"
{
    Properties
    {
        [Header(Color And Brightness)]
        [HDR]_CoreColor("Core HDR Color", Color) = (2.5, 2.8, 5.5, 1.0)
        [HDR]_HotBandColor("Hot Band HDR Color", Color) = (9.0, 8.2, 5.8, 1.0)
        [HDR]_MagneticRimColor("Magnetic Rim HDR Color", Color) = (1.0, 5.0, 10.0, 1.0)
        _Brightness("Brightness", Range(0.0, 30.0)) = 7.0

        [Header(Surface Motion)]
        _NoiseScale("Noise Scale", Range(0.5, 48.0)) = 15.0
        _BandFrequency("Band Frequency", Range(1.0, 32.0)) = 12.0
        _BandSharpness("Band Sharpness", Range(0.25, 16.0)) = 6.0
        _SurfaceFlowSpeed("Surface Flow Speed", Float) = 0.28

        [Header(Pulse)]
        _PulseStrength("Pulse Strength", Range(0.0, 3.0)) = 0.35
        _PulseSpeed("Pulse Speed", Float) = 0.7
        _ExternalPulseIntensity("External Pulse Intensity", Range(0.0, 4.0)) = 1.0

        [Header(Limb Shape)]
        _LimbDarkening("Limb Darkening", Range(0.0, 1.0)) = 0.2
        _RimPower("Rim Power", Range(0.25, 12.0)) = 2.8
        _RimStrength("Rim Strength", Range(0.0, 10.0)) = 2.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PulsarCore"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

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
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 directionOS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _HotBandColor;
                half4 _MagneticRimColor;
                half _Brightness;

                float _NoiseScale;
                float _BandFrequency;
                half _BandSharpness;
                float _SurfaceFlowSpeed;

                half _PulseStrength;
                float _PulseSpeed;
                half _ExternalPulseIntensity;

                half _LimbDarkening;
                half _RimPower;
                half _RimStrength;
            CBUFFER_END

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3d(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0.0, 0.0, 0.0));
                float n100 = hash31(i + float3(1.0, 0.0, 0.0));
                float n010 = hash31(i + float3(0.0, 1.0, 0.0));
                float n110 = hash31(i + float3(1.0, 1.0, 0.0));
                float n001 = hash31(i + float3(0.0, 0.0, 1.0));
                float n101 = hash31(i + float3(1.0, 0.0, 1.0));
                float n011 = hash31(i + float3(0.0, 1.0, 1.0));
                float n111 = hash31(i + float3(1.0, 1.0, 1.0));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);
                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);
                return lerp(nxy0, nxy1, u.z);
            }

            float fbm3d(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                value += noise3d(p) * amplitude;
                p = p * 2.03 + 17.1;
                amplitude *= 0.5;

                value += noise3d(p) * amplitude;
                p = p * 2.01 + 31.7;
                amplitude *= 0.5;

                value += noise3d(p) * amplitude;
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
                output.directionOS = normalize(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float viewFacing = saturate(dot(normalWS, viewDirectionWS));

                float time = _Time.y;
                float3 direction = normalize(input.directionOS);

                float longitude01 = frac(atan2(direction.z, direction.x) / STARFALL_TWO_PI + 0.5);
                float latitude01 = asin(clamp(direction.y, -1.0, 1.0)) / STARFALL_PI + 0.5;
                float polarBias = pow(saturate(abs(direction.y)), 1.7);

                float broadNoise = fbm3d(direction * _NoiseScale + float3(time * _SurfaceFlowSpeed, -time * _SurfaceFlowSpeed * 0.45, time * _SurfaceFlowSpeed * 0.25));
                float fineNoise = fbm3d(direction * _NoiseScale * 2.4 + float3(19.1, time * _SurfaceFlowSpeed * 1.3, 7.2));
                float bandWave = 0.5 + 0.5 * sin((longitude01 * _BandFrequency + latitude01 * 3.0 + broadNoise * 2.0 - time * _SurfaceFlowSpeed) * STARFALL_TWO_PI);
                float bands = pow(saturate(bandWave), max(_BandSharpness, 0.25h));
                float crackle = pow(saturate(1.0 - abs(broadNoise - fineNoise) * 2.1), max(_BandSharpness * 0.75h, 0.25h));
                float heat = saturate(bands * 0.7 + crackle * 0.45 + polarBias * 0.25);

                half pulse = 1.0h + sin(time * _PulseSpeed * STARFALL_TWO_PI) * _PulseStrength * _ExternalPulseIntensity;
                half limb = lerp(1.0h - _LimbDarkening, 1.0h, viewFacing);
                half rim = pow(saturate(1.0h - viewFacing), _RimPower) * _RimStrength;

                half3 color = lerp(_CoreColor.rgb, _HotBandColor.rgb, heat);
                color *= _Brightness * max(pulse, 0.0h) * limb;
                color += _MagneticRimColor.rgb * rim * (0.55h + heat * 0.45h);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
