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

            half4 ReadScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float strength = saturate(_MimicCellBurnScreenStrength);

                if (strength <= 0.001)
                    return ReadScreen(uv);

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

                return color;
            }
            ENDHLSL
        }
    }
}