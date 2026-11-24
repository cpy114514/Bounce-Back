Shader "BounceBack/G Stylized Pool"
{
    Properties
    {
        [MainColor] _BaseColor ("MV_Water · shallow cyan (sRGB)", Color) = (0.45,0.80,0.84,1)
        [MainTexture] _BaseMap ("Unused · keep white", 2D) = "white" {}
        _PoolSize ("Pool width and length (m)", Vector) = (3.25,3.65,0,0)
        _WaveHeight ("Resting geometric wave height (m)", Range(0,0.006)) = 0.006
        _FoamWidth ("White edge foam width (m)", Range(0.025,0.16)) = 0.085
        _Charge ("Elastic charge · 0 to 1", Range(0,1)) = 0
        _ImpactPos ("Impact position · WORLD space", Vector) = (-1.4,0.45,2.05,0)
        _RingWidth ("Impact ring width (m)", Range(0.025,0.20)) = 0.085
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "Stylized Pool Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _PoolSize;
                float4 _ImpactPos;
                half _WaveHeight;
                half _FoamWidth;
                half _Charge;
                half _RingWidth;
            CBUFFER_END
            // Zero means normal engine time. Only the delivery capture tool overrides it.
            float _GPreviewTime;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 waveNormal : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float t = _GPreviewTime > 0.0 ? _GPreviewTime : _Time.y;
                float phase = positionWS.x * 3.2 + positionWS.z * 2.7 + t * 0.66;
                float edge = sin(input.uv.x * PI) * sin(input.uv.y * PI);
                // Only downward displacement: the collision top is never exceeded.
                float wave = (0.5 + 0.5 * sin(phase)) * _WaveHeight * edge;
                float radius = length(positionWS.xz - _ImpactPos.xz);
                float front = _Charge * 3.25;
                float envelope = (1.0 - smoothstep(0.0, 3.4, radius)) * _Charge;
                float ring = sin((radius - front) * 14.0) * exp(-radius * 0.65);
                positionWS.y -= wave + ring * envelope * 0.012;
                float dx = cos(phase) * 3.2 * _WaveHeight * edge;
                float dz = cos(phase) * 2.7 * _WaveHeight * edge;
                output.waveNormal = normalize(float3(dx, 1.0, dz));
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _GPreviewTime > 0.0 ? _GPreviewTime : _Time.y;
                float2 p = (input.uv - 0.5) * _PoolSize.xy;
                float2 warp = float2(sin(p.y * 2.0 + t * 0.31), cos(p.x * 1.8 - t * 0.24)) * 0.24;
                float2 q = p + warp;
                // Two narrow interference contours: restrained, large-scale caustics.
                float caustic = abs(sin(q.x * 4.0 + q.y * 1.7) * sin(q.y * 3.6 - q.x * 1.1));
                caustic = smoothstep(0.90, 0.992, caustic);
                float sweep = 0.5 + 0.5 * sin(p.x * 0.8 + p.y * 0.65 + t * 0.37);
                half3 color = _BaseColor.rgb * lerp(0.91h, 1.07h, sweep);
                color = lerp(color, half3(0.72h,0.93h,0.91h), caustic * 0.28h);

                float edgeDistance = min(min(input.uv.x, 1-input.uv.x) * _PoolSize.x,
                                         min(input.uv.y, 1-input.uv.y) * _PoolSize.y);
                float scallop = sin((p.x+p.y) * 10.0 + t * 0.40) * 0.005;
                float foam = 1.0 - smoothstep(_FoamWidth - 0.018, _FoamWidth + 0.010, edgeDistance + scallop);
                color = lerp(color, half3(0.965h,0.985h,0.945h), foam * 0.92h);

                // A clean expanding front with two decaying trailing concentric rings.
                float radius = length(input.positionWS.xz - _ImpactPos.xz);
                float front = _Charge * 3.25;
                float width = max(_RingWidth, 0.025);
                float primary = 1.0 - smoothstep(width * 0.45, width, abs(radius-front));
                float trailing = 1.0 - smoothstep(width * 0.40, width * 0.85, abs(radius-front+0.22));
                float trail2 = 1.0 - smoothstep(width * 0.32, width * 0.75, abs(radius-front+0.44));
                float ringMask = saturate(primary + trailing * 0.46 + trail2 * 0.20) * _Charge;
                ringMask *= 1.0 - smoothstep(3.0, 3.55, radius);
                color = lerp(color, half3(0.82h,0.985h,0.965h), ringMask * 0.86h);

                half3 lightDirection = normalize(half3(-0.45,0.82,-0.35));
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half highlight = pow(saturate(dot(normalize(input.waveNormal), normalize(lightDirection+viewDirection))), 48.0h);
                color += highlight * 0.045h;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
