Shader "Starfall/3D/TractorBeamFresnel"
{
    Properties
    {
        [HDR]_BeamColor("Beam Color", Color) = (0.2, 0.75, 2.5, 1.0)
        _FresnelPower("Fresnel Power", Range(0.25, 12.0)) = 5.0
        _FresnelStrength("Fresnel Strength", Range(0.0, 8.0)) = 2.0
        _InnerFill("Inner Fill", Range(0.0, 1.0)) = 0.08
        _LengthFadePower("Length Fade Power", Range(0.1, 8.0)) = 1.5
        _TipFade("Tip Fade", Range(0.0, 1.0)) = 0.35
        _Opacity("Opacity", Range(0.0, 4.0)) = 1.0
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
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4 _BeamColor;
                half _FresnelPower;
                half _FresnelStrength;
                half _InnerFill;
                half _LengthFadePower;
                half _TipFade;
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
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                // Bright rim with a faint interior so the beam reads as a soft shell instead of a solid tube.
                half fresnel = pow(saturate(1.0h - dot(normalWS, viewDirectionWS)), _FresnelPower) * _FresnelStrength;

                // Assumes the beam mesh UVs run from ship/base at V=0 to tip at V=1.
                half baseToTip = saturate(input.uv.y);
                half lengthFade = pow(1.0h - baseToTip, _LengthFadePower);
                lengthFade = lerp(lengthFade, 1.0h, 1.0h - _TipFade);

                half beamMask = saturate((fresnel + _InnerFill) * lengthFade);
                half alpha = saturate(beamMask * _Opacity);
                half3 color = _BeamColor.rgb * beamMask;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
