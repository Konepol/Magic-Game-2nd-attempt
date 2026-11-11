// Unlit, glowing (additive) circle texture with a clockwise "draw-on" reveal, used by the casting circles.
// The glow is made from blurred (low mip level) copies of the texture's own alpha, so every stroke gets a soft halo
// and a hotter, whiter core without needing a baked glow in the image.
Shader "Magic/CastingCircle"
{
    Properties
    {
        _BaseMap ("Circle Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Reveal ("Reveal (0 = hidden, 1 = fully drawn)", Range(0, 1)) = 1
        _Soft ("Reveal Edge Softness", Range(0.001, 0.3)) = 0.04
        _Glow ("Stroke Glow", Range(0, 4)) = 1.2
        _CoreWhite ("Hot Core (whitens the stroke centre)", Range(0, 1)) = 0.35
        _Sparkle ("Sparkle Reveal (1 = glyphs pop in one by one)", Range(0, 1)) = 0
        _Pad ("Quad Padding (the quad is this much bigger than the image so the glow is not cut off)", Float) = 1.16
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha One
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
                half _CoreWhite;
                half _Sparkle;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half Halo(float2 uv)
            {
                // two blurs of different widths: a tight inner glow and a wide soft one
                // taps sit closer together than the blur width of the mip they read, so they merge into one smooth glow
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

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half core = tex.a * inside;
                half halo = Halo(uv) * _Glow;

                // angle around the centre, 0 at the top going clockwise to 1
                float2 p = uv - 0.5;
                float t = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);

                half mask = _Reveal >= 0.999 ? 1.0 : saturate((_Reveal - t) / _Soft);
                half flash = 0;

                // sparkle reveal: the image is a grid of 8 x 8 cells holding one glyph each; each cell pops in at its own moment
                if (_Sparkle > 0.5)
                {
                    float2 cell = floor(uv * 8.0);
                    float h = Hash21(cell + 3.7);
                    half local = saturate((_Reveal - h * 0.7) / 0.3);
                    mask = local * (1.0 + 0.6 * (1.0 - local));
                    flash = saturate(local * (1.0 - local) * 4.0);
                    mask *= 0.78 + 0.22 * sin(_Time.y * 7.0 + h * 40.0);
                }

                half3 hot = lerp(_Color.rgb, half3(1, 1, 1), saturate(core * core * core) * _CoreWhite);
                hot = lerp(hot, half3(1, 1, 1), flash * 0.6);
                half3 rgb = hot * (1.0 + 0.25 * _Glow * core);

                half a = saturate(core + halo) * _Color.a * mask;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
