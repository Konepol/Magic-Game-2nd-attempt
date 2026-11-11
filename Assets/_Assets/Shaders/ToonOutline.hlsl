// Outline pass code of ToonShade/Stylized. Included after the shader's HLSLINCLUDE (which declares the properties).
#ifndef TOON_OUTLINE_INCLUDED
#define TOON_OUTLINE_INCLUDED

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float3 uv : TEXCOORD0;      // xy = texture uv; the witch mesh stores its rest-pose position here (see Use Rest Position)
    half4 color : COLOR;
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    half3 tint : TEXCOORD0;
};

float Hash31(float3 p)
{
    p = frac(p * 0.3183099 + 0.1); p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// smooth value noise in 0..1; low frequency gives long swelling and tapering strokes
float Noise3(float3 x)
{
    float3 i = floor(x), f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    float a = lerp(Hash31(i + float3(0, 0, 0)), Hash31(i + float3(1, 0, 0)), f.x);
    float b = lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x);
    float c = lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x);
    float d = lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x);
    return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
}

half3 OutlineTint(half3 albedo)
{
    return lerp(_OutlineColor.rgb, albedo * _OutlineColor.rgb * 2.2, _OutlineFromAlbedo);
}

OutlineVaryings OutlineVert(OutlineAttributes i)
{
    OutlineVaryings o;

    // smoothed normal: hard edges would otherwise tear the outline open
    float3 smoothN = normalize(lerp(normalize(i.normalOS), normalize(i.positionOS.xyz + 1e-5), _OutlineSmooth));
    float3 nWS = TransformObjectToWorldNormal(smoothN);
    float3 nVS = mul((float3x3)UNITY_MATRIX_V, nWS);
    float3 posWS = TransformObjectToWorld(i.positionOS.xyz);

    // pixels -> world units at this vertex's distance, so the hull is a true 3D offset surface
    float viewDepth = abs(mul(UNITY_MATRIX_V, float4(posWS, 1)).z);
    float pixelToWorld = 2.0 * viewDepth / (_ScreenParams.y * max(abs(unity_CameraProjection._m11), 0.01));

    // far away the pixel width would swamp a small object, so beyond a distance the line shrinks with the object
    float3 objPos = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
    float objDist = max(length(objPos - _WorldSpaceCameraPos), 0.01);
    float baseWidth = _OutlineWidth * pixelToWorld * max(saturate(_OutlineFadeStart / objDist), _OutlineFarMin);

    // ink-style weight: long swelling / tapering strokes from smooth noise along the surface
    // Stroke Offset slides this thick/thin pattern along the surface WITHOUT changing how many swells there are (the frequency).
    // Clamped to +-1, so values past that do nothing more; +-1 moves the pattern by two swells.
    float3 patternShift = float3(1.0, 0.6, 0.35) * (clamp(_OutlineOffset, -1.0, 1.0) * 2.0);
    // animated skinned meshes: sample the noise at the rest pose so the thick/thin pattern stays on the cloth instead of swimming
    float3 noisePos = _UseRestPosition > 0.5 ? i.uv : i.positionOS.xyz;
    float swell = Noise3(noisePos * _OutlineVarFreq + patternShift);
    float strokeMul = lerp(_OutlineThinMin, _OutlineThickMax, smoothstep(0.15, 0.85, swell));
    float lineWidth = baseWidth * lerp(1.0, strokeMul, _OutlineVariation);

    // shift: slides the line towards a screen direction (crescent / wedge taper); optionally away from the light
    float2 shift = clamp(float2(_OutlineShiftX, _OutlineShiftY), -1.0, 1.0);
    if (_OutlineShiftFromLight > 0.5)
    {
        float2 away = mul((float3x3)UNITY_MATRIX_V, -_MainLightPosition.xyz).xy;
        shift = normalize(away + 1e-5) * _OutlineShiftAmount;
    }
    float amt = saturate(length(shift));
    float2 nScreen = normalize(nVS.xy + 1e-5);
    float crescent = saturate(dot(nScreen, normalize(shift + 1e-5))) * 2.0;
    lineWidth *= lerp(1.0, crescent, amt);

    float outer = lineWidth;
    posWS += nWS * outer;
    // slide the hull back along the view ray (same screen position, further away): neighbouring surfaces always win the depth test
    posWS += normalize(posWS - _WorldSpaceCameraPos) * outer * _OutlineDepthPush;
    o.positionCS = TransformWorldToHClip(posWS);

    half3 albedo = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(i.uv.xy, _BaseMap), 0).rgb * _BaseColor.rgb
                   * lerp(half3(1, 1, 1), i.color.rgb, _UseVertexColor);
    o.tint = OutlineTint(albedo);
    return o;
}

half4 OutlineFrag(OutlineVaryings i) : SV_Target { return half4(i.tint, 1); }

#endif
