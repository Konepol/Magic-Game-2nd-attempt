// The magic wall at the edge of a map (see MapBounds): a lattice of light with slow bands rising through it, additive.
// It only shows close to the camera, so it appears as you near the edge instead of ringing the whole world.
Shader "Magic/BoundaryVeil"
{
    Properties
    {
        [HDR] _BaseColor ("Colour", Color) = (0.55, 0.75, 1, 0.8)
        [HDR] _AlertColor ("Colour When Outside", Color) = (1, 0.4, 0.6, 1)
        _Alert ("Alert (set by MapBounds)", Range(0, 1)) = 0
        _FadeNear ("Full Strength Within (m)", Float) = 60
        _FadeFar ("Invisible Beyond (m)", Float) = 260
        _Speed ("Band Speed", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Veil"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _AlertColor;
                half _Alert, _FadeNear, _FadeFar, _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = i.uv;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float dist = distance(_WorldSpaceCameraPos, i.positionWS);
                half fade = 1.0 - smoothstep(_FadeNear, _FadeFar, dist);
                if (fade <= 0.001) discard;

                // diamond lattice (uv is in lattice cells)
                float a = abs(frac(i.uv.x + i.uv.y) - 0.5) * 2.0;
                float b = abs(frac(i.uv.x - i.uv.y) - 0.5) * 2.0;
                float w = max(fwidth(i.uv.x + i.uv.y) * 2.0, 0.012);
                half lines = max(smoothstep(1.0 - 0.035 - w, 1.0 - 0.035 + w, a), smoothstep(1.0 - 0.035 - w, 1.0 - 0.035 + w, b));

                // a star twinkling where the lines cross, in some cells
                float2 cell = floor(float2(i.uv.x + i.uv.y, i.uv.x - i.uv.y));
                float2 local = frac(float2(i.uv.x + i.uv.y, i.uv.x - i.uv.y)) - 0.5;
                float h = Hash21(cell);
                half twinkle = h > 0.7 ? saturate(1.0 - length(local) * 5.0) * (0.5 + 0.5 * sin(_Time.y * 3.0 + h * 40.0)) : 0.0;

                // slow bands of light rising through the wall
                half band = 0.5 + 0.5 * sin(i.uv.y * 1.3 - _Time.y * _Speed * 6.0);
                band = band * band * band;

                half3 col = lerp(_BaseColor.rgb, _AlertColor.rgb, _Alert);
                half alpha = (lines * (0.3 + 0.5 * band) + twinkle * 1.2 + 0.03 + 0.05 * band) * fade * lerp(_BaseColor.a, _AlertColor.a, _Alert);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
