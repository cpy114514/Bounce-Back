Shader "Bounce Back/Aegean Backdrop"
{
    Properties
    {
        [NoScaleOffset] _Layers("Cloud and Island Masks", 2D) = "black" {}
        _Zenith("Mist Blue", Color) = (0.57, 0.70, 0.80, 1)
        _Sky("Pale Blue", Color) = (0.69, 0.79, 0.84, 1)
        _Horizon("Warm Ivory", Color) = (0.94, 0.90, 0.81, 1)
        _LowMist("Low Mist", Color) = (0.65, 0.76, 0.80, 1)
        _FarCloud("Far Cloud", Color) = (0.84, 0.86, 0.86, 1)
        _MidCloud("Mid Cloud", Color) = (0.89, 0.92, 0.91, 1)
        _NearCloud("Near Cloud", Color) = (0.93, 0.94, 0.91, 1)
        _Island("Distant Islands", Color) = (0.61, 0.69, 0.75, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Name "Backdrop"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Layers);
            SAMPLER(sampler_Layers);

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Sky, _Horizon, _LowMist;
                half4 _FarCloud, _MidCloud, _NearCloud, _Island;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // The mesh is culled in world space, but the picture is framed in clip space.
                output.positionCS = float4(input.positionOS.xy, UNITY_RAW_FAR_CLIP_VALUE, 1.0);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float heading = UNITY_MATRIX_V[0].z;
                half3 sky = lerp(_Horizon.rgb, _Sky.rgb, smoothstep(0.35, 0.72, uv.y));
                sky = lerp(sky, _Zenith.rgb, smoothstep(0.62, 1.0, uv.y));
                sky = lerp(sky, _LowMist.rgb, 1.0 - smoothstep(0.01, 0.40, uv.y));

                half4 farScene = SAMPLE_TEXTURE2D(_Layers, sampler_Layers, uv + float2(heading * 0.012, 0.0));
                half4 clouds = SAMPLE_TEXTURE2D(_Layers, sampler_Layers, uv + float2(heading * 0.038, 0.0));
                sky = lerp(sky, _Island.rgb, farScene.a * 0.33);
                sky = lerp(sky, _FarCloud.rgb, farScene.r * 0.58);
                sky = lerp(sky, _MidCloud.rgb, clouds.g * 0.67);
                sky = lerp(sky, _NearCloud.rgb, clouds.b * 0.73);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}