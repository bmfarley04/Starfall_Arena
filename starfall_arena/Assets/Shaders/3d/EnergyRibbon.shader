Shader "Custom/EnergyRibbon_URP"
{
    Properties
    {
        [HDR]_BaseColor("Base Color", Color) = (0.25, 1.0, 0.8, 1)
        [HDR]_CoreColor("Core Color", Color) = (1.0, 1.0, 1.0, 1)

        _Alpha("Alpha", Range(0, 2)) = 0.75
        _Brightness("Brightness", Range(0, 20)) = 4.0

        _FlowSpeed("Flow Speed", Float) = 1.5
        _NoiseScale("Noise Scale", Float) = 5.0
        _NoiseStrength("Noise Strength", Range(0, 2)) = 0.8

        _CoreWidth("Core Width", Range(0.01, 1)) = 0.25
        _EdgeFade("Edge Fade", Range(0.1, 8)) = 2.5
        _LengthFade("Length Fade", Range(0.1, 8)) = 1.5

        _DistortionStrength("Vertex Distortion", Range(0, 1)) = 0.12
        _DistortionSpeed("Distortion Speed", Float) = 1.0

        _PulseSpeed("Pulse Speed", Float) = 4.0
        _PulseStrength("Pulse Strength", Range(0, 2)) = 0.4

        _FresnelPower("Fresnel Power", Range(0.1, 8)) = 2.0
        _DepthFadeDistance("Depth Fade Distance", Range(0.01, 5)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "EnergyRibbon"

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 screenPos   : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _CoreColor;

                float _Alpha;
                float _Brightness;

                float _FlowSpeed;
                float _NoiseScale;
                float _NoiseStrength;

                float _CoreWidth;
                float _EdgeFade;
                float _LengthFade;

                float _DistortionStrength;
                float _DistortionSpeed;

                float _PulseSpeed;
                float _PulseStrength;

                float _FresnelPower;
                float _DepthFadeDistance;
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

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(a, b, u.x),
                    lerp(c, d, u.x),
                    u.y
                );
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                value += amplitude * noise2d(p);
                p *= 2.02;
                amplitude *= 0.5;

                value += amplitude * noise2d(p);
                p *= 2.03;
                amplitude *= 0.5;

                value += amplitude * noise2d(p);
                p *= 2.01;
                amplitude *= 0.5;

                value += amplitude * noise2d(p);

                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionOS = input.positionOS.xyz;

                float time = _Time.y;

                float flowingNoise = fbm(float2(
                    input.uv.x * _NoiseScale,
                    input.uv.y * _NoiseScale - time * _DistortionSpeed
                ));

                float sideMask = abs(input.uv.x - 0.5) * 2.0;
                float centerMask = 1.0 - sideMask;

                float displacement =
                    (flowingNoise - 0.5)
                    * _DistortionStrength
                    * centerMask;

                positionOS += input.normalOS * displacement;

                VertexPositionInputs posInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionHCS);

                return output;
            }

            float getDepthFade(float4 screenPos, float3 positionWS)
            {
                float2 screenUV = screenPos.xy / screenPos.w;

                #if UNITY_REVERSED_Z
                    float rawDepth = SampleSceneDepth(screenUV);
                #else
                    float rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(screenUV));
                #endif

                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float objectDepth = LinearEyeDepth(screenPos.z / screenPos.w, _ZBufferParams);

                float fade = saturate((sceneDepth - objectDepth) / _DepthFadeDistance);

                return fade;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;

                float flowY = uv.y - time * _FlowSpeed;

                float n1 = fbm(float2(
                    uv.x * _NoiseScale * 1.5,
                    flowY * _NoiseScale
                ));

                float n2 = fbm(float2(
                    uv.x * _NoiseScale * 4.0 + 12.7,
                    flowY * _NoiseScale * 0.6
                ));

                float noiseCombined = saturate((n1 * 0.75 + n2 * 0.45) * _NoiseStrength);

                float sideDistance = abs(uv.x - 0.5) * 2.0;

                float coreMask = 1.0 - smoothstep(
                    _CoreWidth,
                    _CoreWidth + 0.25,
                    sideDistance
                );

                float edgeMask = pow(saturate(1.0 - sideDistance), _EdgeFade);

                float startFade = smoothstep(0.0, 0.12, uv.y);
                float endFade = 1.0 - smoothstep(0.88, 1.0, uv.y);
                float lengthFade = pow(startFade * endFade, _LengthFade);

                float brokenWisps = smoothstep(0.25, 0.95, noiseCombined);

                float pulse = 1.0 + sin((uv.y * 12.0) - time * _PulseSpeed) * _PulseStrength;
                pulse = max(0.0, pulse);

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(viewDirWS, normalize(input.normalWS))), _FresnelPower);

                float alpha =
                    _Alpha
                    * lengthFade
                    * saturate(edgeMask + coreMask)
                    * brokenWisps;

                alpha *= saturate(0.35 + fresnel);

                float depthFade = getDepthFade(input.screenPos, input.positionWS);
                alpha *= depthFade;

                float3 color = lerp(_BaseColor.rgb, _CoreColor.rgb, coreMask);
                color *= _Brightness;
                color *= pulse;
                color *= saturate(0.5 + noiseCombined);

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}