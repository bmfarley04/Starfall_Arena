Shader "Custom/ProceduralHexShield_ObjectSpace"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _BaseColor ("Hex Line Color", Color) = (0, 0.8, 1, 1)
        [HDR] _FillColor ("Hex Inside Color (Set Black for Empty)", Color) = (0, 0.1, 0.2, 0)
        
        [Header(Hexagon Grid)]
        _HexScale ("Hex Scale", Float) = 2.0
        _HexLineWidth ("Hex Line Width", Range(0, 1)) = 0.05
        _HexSharpness ("Hex Edge Sharpness", Range(1, 100)) = 20.0
        _ScanningSpeed ("Scanning Speed", Vector) = (0.5, 0.5, 0.5, 0)
        
        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3.0
        [HDR] _FresnelColor ("Fresnel Rim Color", Color) = (0, 0.5, 1, 1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        // Blend One One = Pure Additive (Black is transparent, Colors add up)
        Blend One One
        ZWrite Off
        Cull Off // Render back and front faces

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _FillColor;
                float _HexScale;
                float _HexLineWidth;
                float _HexSharpness;
                float4 _ScanningSpeed;
                float _FresnelPower;
                float4 _FresnelColor;
            CBUFFER_END

            // --- Procedural Hex Logic ---
            float HexDist(float2 p) {
                p = abs(p);
                float c = dot(p, normalize(float2(1, 1.73)));
                c = max(c, p.x);
                return c;
            }

            float4 GetHex(float2 uv) {
                float2 r = float2(1, 1.73);
                float2 h = r * 0.5;
                float2 a = fmod(abs(uv), r) - h; // abs(uv) mirrors it to fix negative coord issues
                float2 b = fmod(abs(uv) - h, r) - h;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                float y = 0.5 - HexDist(gv);
                return float4(0, y, 0, 0);
            }

            float GetGridIntensity(float2 uv) {
                float4 hexData = GetHex(uv);
                float distToEdge = hexData.y;
                // smoothstep: 0 = line, 1 = center. We invert at the end.
                float hexGrid = smoothstep(_HexLineWidth, _HexLineWidth + (1.0/_HexSharpness), distToEdge);
                return 1.0 - hexGrid; 
            }
            // ----------------------------

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                
                // Pass Object Space Position to Fragment
                OUT.positionOS = IN.positionOS.xyz;
                
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.normalWS);
                float3 viewDir = normalize(IN.viewDirWS);
                
                // --- Triplanar Mapping (Object Space) ---
                // We use the object's local coordinates. This keeps the pattern
                // tied to the shield size, not the world size.
                
                // 1. Calculate Blend Weights (so the texture projects from the correct side)
                float3 weights = abs(normal);
                weights = max(weights - 0.2, 0); 
                weights /= dot(weights, float3(1,1,1));

                // 2. Project Grid on 3 planes
                float3 scroll = _Time.y * _ScanningSpeed.xyz;
                
                // Note: We use positionOS (Object Space) * Scale
                float2 uvX = (IN.positionOS.yz * _HexScale) + scroll.yz;
                float2 uvY = (IN.positionOS.xz * _HexScale) + scroll.xz;
                float2 uvZ = (IN.positionOS.xy * _HexScale) + scroll.xy;

                float gridX = GetGridIntensity(uvX);
                float gridY = GetGridIntensity(uvY);
                float gridZ = GetGridIntensity(uvZ);

                // 3. Blend them
                float hexLines = gridX * weights.x + gridY * weights.y + gridZ * weights.z;

                // --- Colors ---
                // If hexLines is 1 (Line), use BaseColor.
                // If hexLines is 0 (Inside), use FillColor.
                // We Lerp (blend) between them based on the line strength.
                float3 gridVisual = lerp(_FillColor.rgb, _BaseColor.rgb, hexLines);

                // --- Fresnel ---
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                float3 fresnelVisual = fresnel * _FresnelColor.rgb;

                // --- Combine ---
                // Additive blend means we just add the colors together.
                // gridVisual + fresnelVisual
                return half4(gridVisual + fresnelVisual, 1);
            }
            ENDHLSL
        }
    }
}