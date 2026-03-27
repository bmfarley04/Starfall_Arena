Shader "UI/ProceduralCrosshair"
{
    Properties
    {
        _Color ("Crosshair Color", Color) = (1, 1, 1, 1)

        [Header(Center Dot)]
        _DotRadius ("Dot Radius", Range(0.0, 0.1)) = 0.025
        _DotSoftness ("Dot Edge Softness", Range(0.001, 0.02)) = 0.005

        [Header(Inner Ring)]
        _RingRadius ("Ring Radius", Range(0.0, 0.3)) = 0.11
        _RingThickness ("Ring Thickness", Range(0.001, 0.05)) = 0.015
        _RingSoftness ("Ring Edge Softness", Range(0.001, 0.02)) = 0.005
        _SpinSpeed ("Ring Spin Speed", Float) = 0.0

        [Header(Outer Arcs)]
        _ArcInnerRadius ("Arc Inner Radius", Range(0.1, 0.5)) = 0.22
        _ArcOuterRadius ("Arc Outer Radius", Range(0.1, 0.5)) = 0.28
        _ArcSoftness ("Arc Edge Softness", Range(0.001, 0.02)) = 0.005
        _GapAngle ("Gap Angle (degrees)", Range(1, 45)) = 18.0
        _FillAmount ("Fill Amount", Range(0.0, 1.0)) = 1.0

        [Header(Tick Marks on Ring)]
        _ShowTicks ("Show Tick Marks", Range(0, 1)) = 1.0
        _TickCount ("Tick Count", Int) = 4
        _TickLength ("Tick Length", Range(0.0, 0.05)) = 0.02
        _TickWidth ("Tick Angular Width (degrees)", Range(0.5, 10)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // Properties
            fixed4 _Color;

            float _DotRadius;
            float _DotSoftness;

            float _RingRadius;
            float _RingThickness;
            float _RingSoftness;
            float _SpinSpeed;

            float _ArcInnerRadius;
            float _ArcOuterRadius;
            float _ArcSoftness;
            float _GapAngle;
            float _FillAmount;

            float _ShowTicks;
            int _TickCount;
            float _TickLength;
            float _TickWidth;

            // ── Constants ──
            #define PI 3.14159265359
            #define TAU 6.28318530718

            // ── Helpers ──

            // Antialiased ring SDF: returns 1 inside the ring, 0 outside
            float ring(float dist, float radius, float thickness, float softness)
            {
                float halfThick = thickness * 0.5;
                float outer = 1.0 - smoothstep(radius + halfThick - softness, radius + halfThick, dist);
                float inner = smoothstep(radius - halfThick, radius - halfThick + softness, dist);
                return outer * inner;
            }

            // Antialiased filled circle
            float circle(float dist, float radius, float softness)
            {
                return 1.0 - smoothstep(radius - softness, radius, dist);
            }

            // Remap angle from [-PI, PI] to [0, 1]
            float angleToNorm(float angle)
            {
                return (angle + PI) / TAU;
            }

            // Rotate UV around origin
            float2 rotateUV(float2 uv, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(
                    uv.x * c - uv.y * s,
                    uv.x * s + uv.y * c
                );
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Center the UV so (0,0) is the middle of the quad
                float2 centeredUV = i.uv - 0.5;

                // ════════════════════════════════════
                //  INNER ELEMENTS (affected by spin)
                // ════════════════════════════════════
                float2 spinUV = rotateUV(centeredUV, _Time.y * _SpinSpeed);
                float spinDist = length(spinUV);
                float spinAngle = atan2(spinUV.y, spinUV.x);

                // Center dot
                float dotMask = circle(spinDist, _DotRadius, _DotSoftness);

                // Inner ring
                float ringMask = ring(spinDist, _RingRadius, _RingThickness, _RingSoftness);

                // Tick marks on the ring (spin with it)
                float tickMask = 0.0;
                if (_ShowTicks > 0.5)
                {
                    float tickAngularWidth = _TickWidth * PI / 180.0;

                    for (int t = 0; t < 12; t++) // max 12 ticks
                    {
                        if (t >= _TickCount) break;

                        float tickAngle = (float(t) / float(_TickCount)) * TAU - PI;
                        float angleDiff = abs(spinAngle - tickAngle);
                        // Wrap
                        angleDiff = min(angleDiff, TAU - angleDiff);

                        float angularMask = 1.0 - smoothstep(tickAngularWidth * 0.5 - _RingSoftness,
                                                              tickAngularWidth * 0.5, angleDiff);

                        float tickOuter = _RingRadius + _RingThickness * 0.5 + _TickLength;
                        float tickInner = _RingRadius - _RingThickness * 0.5 - _TickLength;
                        float radialMask = smoothstep(tickInner, tickInner + _RingSoftness, spinDist)
                                         * (1.0 - smoothstep(tickOuter - _RingSoftness, tickOuter, spinDist));

                        tickMask = max(tickMask, angularMask * radialMask);
                    }
                }

                // ════════════════════════════════════
                //  OUTER ARCS (static, with fill)
                // ════════════════════════════════════
                float dist = length(centeredUV);
                float angle = atan2(centeredUV.y, centeredUV.x); // [-PI, PI]

                // Radial band for arcs
                float arcRadial = smoothstep(_ArcInnerRadius, _ArcInnerRadius + _ArcSoftness, dist)
                                * (1.0 - smoothstep(_ArcOuterRadius - _ArcSoftness, _ArcOuterRadius, dist));

                // Each arc spans (90 - gapAngle) degrees, centered on cardinal directions.
                // Cardinal angles: 0 (right), PI/2 (up), PI (left), -PI/2 (down)
                float gapRad = _GapAngle * PI / 180.0;
                float halfArc = (PI * 0.5 - gapRad) * 0.5; // half angular span of one arc segment
                float halfGap = gapRad * 0.5;

                float arcMask = 0.0;

                // We define 4 arcs at cardinals: right(0), top(PI/2), left(PI), bottom(-PI/2)
                float cardinals[4];// = { 0.0, PI * 0.5, PI, -PI * 0.5 };
                cardinals[0] = 0.0;
                cardinals[1] = PI * 0.5;
                cardinals[2] = PI;
                cardinals[3] = -PI * 0.5;

                for (int a = 0; a < 4; a++)
                {
                    float center = cardinals[a];

                    // Signed angular distance from this cardinal direction
                    float diff = angle - center;

                    // Wrap to [-PI, PI]
                    diff = diff - TAU * round(diff / TAU);

                    float absDiff = abs(diff);

                    // Full arc spans from halfGap to halfGap + 2*halfArc centered on cardinal
                    // Actually each arc segment spans halfArc on each side of the cardinal
                    // with the gap eating into the edges toward neighboring cardinals.

                    // Arc exists where |diff| < halfArc
                    // But we want fill animation: the arc fills FROM the center of the segment outward
                    // (or you could reverse this — adjust to taste)

                    float arcSpan = halfArc; // full half-span when fill = 1

                    // Fill: scale the visible half-span by FillAmount
                    float visibleHalfSpan = arcSpan * _FillAmount;

                    float angMask = 1.0 - smoothstep(visibleHalfSpan - _ArcSoftness,
                                                     visibleHalfSpan, absDiff);

                    arcMask = max(arcMask, angMask);
                }

                float finalArcMask = arcRadial * arcMask;

                // ════════════════════════════════════
                //  COMPOSITE
                // ════════════════════════════════════
                float compositeMask = saturate(dotMask + ringMask + tickMask + finalArcMask);

                fixed4 col = _Color;
                col.a *= compositeMask;

                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}
