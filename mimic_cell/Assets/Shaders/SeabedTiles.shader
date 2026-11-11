Shader "Mimic/Seabed Tiles"
{
    Properties
    {
        _Tile0 ("Tile 1", 2D) = "white" {}
        _Tile1 ("Tile 2", 2D) = "white" {}
        _Tile2 ("Tile 3", 2D) = "white" {}
        _Tile3 ("Tile 4", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _TileSeed ("Tile Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Tile0); SAMPLER(sampler_Tile0);
            TEXTURE2D(_Tile1); SAMPLER(sampler_Tile1);
            TEXTURE2D(_Tile2); SAMPLER(sampler_Tile2);
            TEXTURE2D(_Tile3); SAMPLER(sampler_Tile3);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _TileSeed;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            uint TileIndex(float2 uv)
            {
                uint2 cell = asuint((int2)floor(uv));
                uint hash = (cell.x * 0x9e3779b1u) ^ (cell.y * 0x85ebca77u) ^ (uint)_TileSeed;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                return hash & 3u;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 tileUV = frac(input.uv);
                // Use the continuous UV gradients across tile edges for mip filtering.
                float2 dx = ddx(input.uv);
                float2 dy = ddy(input.uv);
                half4 color;
                switch (TileIndex(input.uv))
                {
                    case 0u: color = SAMPLE_TEXTURE2D_GRAD(_Tile0, sampler_Tile0, tileUV, dx, dy); break;
                    case 1u: color = SAMPLE_TEXTURE2D_GRAD(_Tile1, sampler_Tile1, tileUV, dx, dy); break;
                    case 2u: color = SAMPLE_TEXTURE2D_GRAD(_Tile2, sampler_Tile2, tileUV, dx, dy); break;
                    default: color = SAMPLE_TEXTURE2D_GRAD(_Tile3, sampler_Tile3, tileUV, dx, dy); break;
                }

                return color * _Color;
            }
            ENDHLSL
        }
    }
}
