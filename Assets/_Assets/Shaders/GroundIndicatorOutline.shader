// A soft dark copy of a ground indicator layer, drawn underneath the glowing layer so the lines stay readable on pale ground
// (a purely additive glow washes out to white on sand or snow). Normal alpha blending, black, strength in the colour's alpha.
// It spreads from the texture's own alpha the same way the glow does (blurred, low mip copies), and follows the same reveal modes:
// 0 clockwise draw-on, 3 outward from the middle, 4 from the bottom edge up.
Shader "Magic/GroundIndicatorOutline"
{
    Properties
    {
        _BaseMap ("Layer Texture", 2D) = "white" {}
        _Color ("Colour (alpha = strength)", Color) = (0, 0, 0, 0.6)
        _Reveal ("Reveal (0 = hidden, 1 = fully drawn)", Range(0, 1)) = 1
        _Soft ("Reveal Edge Softness", Range(0.001, 0.3)) = 0.04
        _Glow ("Spread", Range(0, 4)) = 1.3
        _Mode ("Reveal Mode (0 clockwise, 3 radial, 4 bottom up)", Range(0, 4)) = 0
        _Pad ("Quad Padding (the quad is this much bigger than the image)", Float) = 1.16
    }

    SubShader
    {
        // drawn just before the other transparent things, so the glow lands on top of it
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half _Reveal;
                half _Soft;
                half _Glow;
                half _Mode;
                half _Pad;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // the quad is _Pad times bigger than the image; map it back so the image keeps its size
                output.uv = (TRANSFORM_TEX(input.uv, _BaseMap) - 0.5) * _Pad + 0.5;
                return output;
            }

            half Spread(float2 uv)
            {
                half h1 = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 2.5).a;
                half h2 = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 4.0).a;
                const float2 d[8] = { float2(1, 0), float2(0.707, 0.707), float2(0, 1), float2(-0.707, 0.707),
                                      float2(-1, 0), float2(-0.707, -0.707), float2(0, -1), float2(0.707, -0.707) };
                [unroll] for (int i = 0; i < 8; i++)
                {
                    h1 += SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv + d[i] * 0.008, 2.5).a;
                    h2 += SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv + d[i] * 0.020, 4.0).a;
                }
                return h1 / 9.0 * 0.8 + h2 / 9.0 * 0.7;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half inside = (uv.x > 0 && uv.x < 1 && uv.y > 0 && uv.y < 1) ? 1 : 0;

                half core = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * inside;

                float2 p = uv - 0.5;
                float r = length(p) / 0.5;
                // fades to nothing before the edge of the image, like the glow
                half spread = Spread(uv) * _Glow * inside * saturate((0.5 - length(p)) / 0.03);

                half mask = 1.0;
                if (_Mode < 0.5)
                {
                    // angle around the centre, 0 at the top going clockwise to 1
                    float t = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                    mask = _Reveal >= 0.999 ? 1.0 : saturate((_Reveal - t) / _Soft);
                }
                else if (_Mode < 3.5)
                {
                    mask = saturate((_Reveal * 1.15 - r) / 0.08);
                }
                else
                {
                    mask = _Reveal >= 0.999 ? 1.0 : saturate((_Reveal * 1.1 - uv.y) / 0.06);
                }

                half a = saturate(core + spread) * _Color.a * mask;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
