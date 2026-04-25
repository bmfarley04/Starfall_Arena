Shader "Starfall/3D/ProceduralHexArenaBoundary"
{
    Properties
    {
        _HexMask ("Hex Mask", 2D) = "white" {}
        [HDR] _ProximityColor ("Proximity Color", Color) = (0.1, 4, 5, 1)
        [HDR] _ShrinkColor ("Shrink Color", Color) = (5, 0.2, 0.05, 1)
        _TextureWorldSize ("Texture World Size", Float) = 32
        _MaskThreshold ("Mask Threshold", Range(0, 1)) = 0.15
        _MaskSoftness ("Mask Softness", Range(0.001, 0.5)) = 0.08
        _MaskPower ("Mask Power", Range(0.25, 4)) = 1
        _PulseSpeed ("Pulse Speed", Float) = 2
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.25
        _CrackleScale ("Crackle Scale", Float) = 0.65
        _CrackleSpeed ("Crackle Speed", Float) = 3
        _CrackleStrength ("Crackle Strength", Range(0, 1)) = 0.2
        _RevealDistance ("Reveal Distance", Float) = 14
        _VisiblePatchRadius ("Visible Patch Radius", Float) = 8
        _IdleVisibility ("Idle Visibility", Range(0, 1)) = 0
        _ProximityVisibility ("Proximity Visibility", Range(0, 1)) = 0.9
        _ShrinkVisibility ("Shrink Visibility", Range(0, 1)) = 0
        _IsShrinking ("Is Shrinking", Float) = 0
        _ShrinkPulse ("Shrink Pulse", Float) = 0
        _Active ("Active", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Textured Hex Boundary"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_HexMask);
            SAMPLER(sampler_HexMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _ProximityColor;
                half4 _ShrinkColor;
                float4 _HexMask_ST;
                float _TextureWorldSize;
                float _MaskThreshold;
                float _MaskSoftness;
                float _MaskPower;
                float _PulseSpeed;
                float _PulseStrength;
                float _CrackleScale;
                float _CrackleSpeed;
                float _CrackleStrength;
                float _RevealDistance;
                float _VisiblePatchRadius;
                float _IdleVisibility;
                float _ProximityVisibility;
                float _ShrinkVisibility;
                float _IsShrinking;
                float _ShrinkPulse;
                float _Active;
                float _RevealSampleCount;
            CBUFFER_END

            float4 _RevealCenters[12];
            float _RevealDistances[12];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            float ViewerReveal(float3 positionWS, float3 revealCenterWS, float wallDistance)
            {
                float approach = 1.0 - saturate(wallDistance / max(_RevealDistance, 0.001));
                float patchDistance = distance(positionWS, revealCenterWS);
                float patch = 1.0 - saturate(patchDistance / max(_VisiblePatchRadius, 0.001));
                return smoothstep(0.0, 1.0, approach) * smoothstep(0.0, 1.0, patch);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 cell = floor(uv);
                float2 local = frac(uv);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_Active <= 0.0)
                {
                    discard;
                }

                float2 maskUv = input.uv / max(_TextureWorldSize, 0.001);
                maskUv = maskUv * _HexMask_ST.xy + _HexMask_ST.zw;
                half4 maskSample = SAMPLE_TEXTURE2D(_HexMask, sampler_HexMask, maskUv);
                half maskBrightness = dot(maskSample.rgb, half3(0.299, 0.587, 0.114));
                half mask = smoothstep(_MaskThreshold, _MaskThreshold + max(_MaskSoftness, 0.001), maskBrightness);
                mask = pow(saturate(mask), max(_MaskPower, 0.001));

                float reveal = 0.0;
                [unroll]
                for (int i = 0; i < 12; i++)
                {
                    if (i < _RevealSampleCount)
                    {
                        reveal = max(reveal, ViewerReveal(input.positionWS, _RevealCenters[i].xyz, _RevealDistances[i]));
                    }
                }

                float proximityVisibility = lerp(_IdleVisibility, _ProximityVisibility, reveal);
                float shrinkVisibility = _IsShrinking > 0.5 ? _ShrinkVisibility : 0.0;
                float visibility = saturate(max(proximityVisibility, shrinkVisibility)) * mask;

                half shrinkBlend = (half)saturate(_IsShrinking * (0.35 + _ShrinkPulse * 0.65));
                half3 color = lerp(_ProximityColor.rgb, _ShrinkColor.rgb, shrinkBlend);
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed + input.uv.x * 0.08 + input.uv.y * 0.05) * _PulseStrength;

                float2 crackleUvA = input.uv * max(_CrackleScale, 0.001) + float2(_Time.y * _CrackleSpeed, -_Time.y * _CrackleSpeed * 0.37);
                float2 crackleUvB = input.uv * max(_CrackleScale * 1.83, 0.001) + float2(-_Time.y * _CrackleSpeed * 0.23, _Time.y * _CrackleSpeed * 0.61);
                float crackleNoise = ValueNoise(crackleUvA) * ValueNoise(crackleUvB);
                float crackleSparks = smoothstep(0.42, 0.9, crackleNoise);

                float diagonalFlow = frac(input.uv.x * 0.035 + input.uv.y * 0.075 + _Time.y * _CrackleSpeed * 0.8);
                float diagonalStreak = 1.0 - smoothstep(0.0, 0.08, abs(diagonalFlow - 0.5));
                float crackle = saturate(crackleSparks * 0.65 + diagonalStreak * crackleSparks) * _CrackleStrength;

                half energy = (half)max(0.0, pulse + crackle * 2.75);
                half alpha = (half)saturate(visibility * (1.0 + crackle * 2.0));
                return half4(color * visibility * energy, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
