Shader "Hidden/BounceBack/Construction Preview"
{
    Properties { _Color("Tint", Color) = (1, 1, 1, 0.4) }
    SubShader
    {
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct Varyings
            {
                float4 position : SV_POSITION;
                half3 normal : TEXCOORD0;
            };

            Varyings Vert(appdata_base vertex)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(vertex.vertex);
                output.normal = UnityObjectToWorldNormal(vertex.normal);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                half lighting = 0.68 + 0.32 * saturate(dot(normalize(input.normal), half3(0.35, 0.81, 0.47)));
                return fixed4(_Color.rgb * lighting, _Color.a);
            }
            ENDCG
        }
    }
}
