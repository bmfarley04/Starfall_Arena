Shader "Custom/RingOfFireMask"
{
    Properties
    {
        _Color ("Mask Color", Color) = (1, 0, 0, 0.5)
        _ShapeType ("Shape Type", Float) = 0 // 0 = Box, 1 = Circle
        _SafeCenter ("Safe Zone Center", Vector) = (0, 0, 0, 0)
        _SafeSize ("Safe Zone Size", Vector) = (10, 10, 5, 0) // x=width, y=length, z=radius
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            Name "RingOfFireMask"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _ShapeType;
                float4 _SafeCenter;
                float4 _SafeSize;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 worldPos2D = input.positionWS.xy;
                float2 centerPos = _SafeCenter.xy;

                float isInSafeZone = 0.0;

                if (_ShapeType < 0.5) // Box
                {
                    float halfWidth = _SafeSize.x * 0.5;
                    float halfLength = _SafeSize.y * 0.5;

                    float2 minBounds = centerPos - float2(halfWidth, halfLength);
                    float2 maxBounds = centerPos + float2(halfWidth, halfLength);

                    // Check if inside box
                    if (worldPos2D.x >= minBounds.x && worldPos2D.x <= maxBounds.x &&
                        worldPos2D.y >= minBounds.y && worldPos2D.y <= maxBounds.y)
                    {
                        isInSafeZone = 1.0;
                    }
                }
                else // Circle
                {
                    float radius = _SafeSize.z;
                    float2 delta = worldPos2D - centerPos;
                    float distSq = dot(delta, delta);

                    // Check if inside circle
                    if (distSq <= radius * radius)
                    {
                        isInSafeZone = 1.0;
                    }
                }

                // Only render outside the safe zone
                half4 col = _Color;
                col.a *= (1.0 - isInSafeZone);

                return col;
            }
            ENDHLSL
        }
    }
}
