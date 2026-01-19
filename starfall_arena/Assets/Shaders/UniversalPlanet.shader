Shader "Custom/UniversalPlanet"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _MainTex ("Planet Texture", 2D) = "white" {}
        _Smoothness ("Smoothness (0=Dust, 1=Ice/Glass)", Range(0, 1)) = 0.2
        _SpecularColor ("Specular Color", Color) = (0.2, 0.2, 0.2, 1)

        [Header(Auto Detail)]
        _BumpScale ("Fake Height Strength", Range(0, 5)) = 2.0
        
        [Header(Atmosphere)]
        [HDR] _RimColor ("Atmosphere Color", Color) = (0.2, 0.5, 1, 1)
        _RimPower ("Atmosphere Power (Falloff)", Range(0.5, 10.0)) = 4.0
        _RimIntensity ("Atmosphere Intensity", Range(0, 10)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _BumpScale;
                half _Smoothness;
                half4 _SpecularColor;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
            CBUFFER_END

            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);

            // Helper to get brightness
            float GetLuminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1,1,1,1));

                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                
                // 1. Sample Texture
                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float brightness = GetLuminance(albedo.rgb);

                // 2. Auto-Generate Normals (The "Fake Height")
                // Uses neighboring pixel brightness to create a slope
                float2 dUV = float2(ddx(brightness), ddy(brightness)) * _BumpScale * 10.0;
                
                // Perturb Normal
                // We use a simplified tangent math here to avoid needing complex mesh tangents
                float3 bitangent = cross(normalWS, float3(0,1,0));
                float3 tangent = cross(bitangent, normalWS);
                normalWS = normalize(normalWS + (tangent * -dUV.x) + (bitangent * -dUV.y));

                // 3. Lighting Data
                float3 viewDirWS = GetWorldSpaceViewDir(input.positionWS);
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float shadow = mainLight.shadowAttenuation; 

                // 4. Lighting Calculation (Lambert)
                float NdotL = saturate(dot(normalWS, lightDir));
                float lightIntensity = NdotL * shadow;

                // 5. Atmosphere (Rim)
                // Strictly masked by light direction (NdotL) so it doesn't appear on night side
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _RimPower);
                float3 rim = _RimColor.rgb * fresnel * _RimIntensity * NdotL;

                // 6. Specular (Reflection)
                float3 halfVector = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfVector));
                
                // Simple Blinn-Phong Specular
                float specular = pow(NdotH, 1.0 / (1.0 - _Smoothness + 0.01)) * _Smoothness * shadow;
                float3 specColor = _SpecularColor.rgb * specular;

                // 7. Combine
                float3 finalSurface = (albedo.rgb * mainLight.color * lightIntensity) + specColor;
                
                return half4(finalSurface + rim, 1.0);
            }
            ENDHLSL
        }
    }
}