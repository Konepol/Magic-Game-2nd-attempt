// A soft round shadow laid on the ground under a character (for the "blob shadow" style). Darkens what is below it toward Tint.
Shader "Magic/BlobShadow"
{
    Properties
    {
        _BaseMap ("Falloff", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.45, 0.45, 0.72, 1)
        _Strength ("Strength", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Blob"
            Blend DstColor Zero
            ZWrite Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _Strength * i.color.a;
                return half4(lerp(half3(1, 1, 1), _Tint.rgb, a), 1);
            }
            ENDHLSL
        }
    }
}
