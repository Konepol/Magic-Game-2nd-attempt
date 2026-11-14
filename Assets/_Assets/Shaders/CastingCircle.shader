// Unlit, glowing (additive) circle texture used by the casting circles.
// The glow is made from blurred (low mip level) copies of the texture's own alpha, so every stroke gets a soft halo
// and a hotter, whiter core without needing a baked glow in the image.
// _Mode picks how the image is revealed:  0 clockwise draw-on, 1 glyphs pop in at random (8 x 8 grid cells),
// 2 glyphs pop in from the middle outward (cells), 3 a ring of reveal that travels from the middle outward.
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
        _Mode ("Reveal Mode (0 clockwise, 1 random glyphs, 2 glyphs from the middle, 3 radial)", Range(0, 3)) = 0
        _Dissolve ("Dissolve (0 = whole, 1 = gone)", Range(0, 1)) = 0
        _Stretch ("Stretch Outward (the lines are pulled away from the middle)", Range(0, 1)) = 0
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
                half _Mode;
                half _Dissolve;
                half _Stretch;
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

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            half Halo(float2 uv)
            {
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
                // exit: the lines are pulled outward from the middle
                uv = 0.5 + (uv - 0.5) / (1.0 + _Stretch * 0.9);

                half inside = (uv.x > 0 && uv.x < 1 && uv.y > 0 && uv.y < 1) ? 1 : 0;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half core = tex.a * inside;

                float2 p = uv - 0.5;
                float r = length(p) / 0.5;      // 0 in the middle, 1 at the edge of the image
                // the halo fades to nothing before the edge of the image, so the border pixels never show as flat patches
                half halo = Halo(uv) * _Glow * inside * saturate((0.5 - length(p)) / 0.03);

                half mask = 1.0;
                half flash = 0;

                if (_Mode < 0.5)
                {
                    // angle around the centre, 0 at the top going clockwise to 1
                    float t = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                    mask = _Reveal >= 0.999 ? 1.0 : saturate((_Reveal - t) / _Soft);
                }
                else if (_Mode < 2.5)
                {
                    // glyph grid: each 8 x 8 cell pops in at its own moment (random, or in order of distance from the middle)
                    float2 cell = floor(uv * 8.0);
                    float h = Hash21(cell + 3.7);
                    float key = _Mode < 1.5 ? h * 0.7 : saturate((length((cell + 0.5) / 8.0 - 0.5) / 0.5) * 0.8 + h * 0.25);
                    half local = saturate((_Reveal * (_Mode < 1.5 ? 1.0 : 1.3) - key) / (_Mode < 1.5 ? 0.3 : 0.25));
                    mask = local * (1.0 + 0.6 * (1.0 - local));
                    flash = saturate(local * (1.0 - local) * 4.0);
                    mask *= 0.78 + 0.22 * sin(_Time.y * 7.0 + h * 40.0);
                }
                else
                {
                    // a reveal that travels outward from the middle; the leading edge burns brighter
                    float front = _Reveal * 1.15;
                    mask = saturate((front - r) / 0.08);
                    flash = _Reveal < 0.999 ? saturate(1.0 - abs(front - r) / 0.1) * 0.7 : 0.0;
                }

                // dissolve: patches of the circle burn away; a bright rim shows on the patches that are about to go
                half rim = 0;
                if (_Dissolve > 0.001)
                {
                    float n = ValueNoise(uv * 26.0) * 0.7 + ValueNoise(uv * 70.0) * 0.3;
                    float th = lerp(-0.1, 1.1, _Dissolve);
                    mask *= saturate((n - th) / 0.06);
                    rim = saturate(1.0 - abs(n - th - 0.05) / 0.08);
                }

                half3 hot = lerp(_Color.rgb, half3(1, 1, 1), saturate(core * core * core) * _CoreWhite);
                hot = lerp(hot, half3(1, 1, 1), max(flash * 0.6, rim * 0.8));
                half3 rgb = hot * (1.0 + 0.25 * _Glow * core);

                half a = saturate(core + halo) * _Color.a * mask;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
