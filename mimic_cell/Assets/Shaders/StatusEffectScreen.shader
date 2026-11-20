Shader "MimicCell/Burn Heat Distortion"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _MimicCellBurnScreenStrength;
            float _MimicCellPressureScreenStrength;
            float _MimicCellAssimilation;

            half4 ReadScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }
half4 ApplyAssimilationDarkening(half4 color, float2 screenUV)
{
    float assimilation = saturate(_MimicCellAssimilation);

    float amount = smoothstep(0.60, 0.95, assimilation);
    float2 fromCenter = (screenUV - 0.5) * 2.0;
    float edge = smoothstep(0.65, 1.3, length(fromCenter));

    float edgeDarkness = edge * amount * 0.25;
    float fullDarkness = smoothstep(0.95, 1.0, assimilation);

    color.rgb *= (1.0 - edgeDarkness) * (1.0 - fullDarkness);
    return color;
}
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

float pressure = saturate(_MimicCellPressureScreenStrength);
float2 centered = uv - 0.5;
float edgeDistance = saturate(max(abs(centered.x), abs(centered.y)) * 2.0);
float squeeze = edgeDistance * edgeDistance * (1.0 - edgeDistance);
uv += centered * squeeze * (0.4 * pressure);

float strength = saturate(_MimicCellBurnScreenStrength);
if (strength <= 0.001)
    return ApplyAssimilationDarkening(ReadScreen(uv), input.texcoord);
                float2 fromCenter = abs(uv - 0.5) * 2.0;
                float edge = smoothstep(0.25, 1.0, max(fromCenter.x, fromCenter.y));
                float time = _Time.y;

                float2 waves = float2(
                    sin(uv.y * 42.0 - time * 4.0)
                        + 0.35 * sin(uv.y * 83.0 + time * 2.0),
                    sin(uv.x * 29.0 + time * 3.0)
                );

                float2 warpedUV = uv + waves * (0.002 + 0.009 * edge) * strength;

                float2 blur = _BlitTexture_TexelSize.xy
                    * (3.0 + 5.0 * edge) * strength;

                float2 horizontal = float2(blur.x, 0.0);
                float2 vertical = float2(0.0, blur.y);

                half4 color = ReadScreen(warpedUV) * 0.40;
                color += ReadScreen(warpedUV + horizontal) * 0.15;
                color += ReadScreen(warpedUV - horizontal) * 0.15;
                color += ReadScreen(warpedUV + vertical) * 0.15;
                color += ReadScreen(warpedUV - vertical) * 0.15;

                color.rgb = lerp(
                    color.rgb, half3(1.0, 0.78, 0.65),
                    strength * (0.04 + 0.08 * edge));

return ApplyAssimilationDarkening(color, input.texcoord);            }
            ENDHLSL
        }
    }
}