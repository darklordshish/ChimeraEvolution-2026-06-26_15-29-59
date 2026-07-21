// Звуковая волна: расходящаяся СФЕРА, видимая СКВОЗЬ стены (звук идёт сквозь — ZTest Always).
// Рисуется как оболочка: непрозрачность растёт к краю силуэта (френель), поэтому шар читается
// пузырём, а не залитой кляксой и не загораживает мир. Радиус даёт масштаб объекта, силу — альфа.
// Материал создаёт NoiseWaves через Shader.Find — при сборке билда добавить в Always Included Shaders.
Shader "Chimera/NoiseWave"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.3)
        _Rim ("Rim Power", Range(0.5, 6)) = 2.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+90" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZTest Always
        ZWrite Off
        Blend SrcAlpha One   // аддитивно — «светится», не пачкает картинку
        Cull Front           // изнутри тоже видно: игрок может оказаться внутри волны

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Rim;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 view : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.view = normalize(_WorldSpaceCameraPos - world);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // френель: в центре силуэта прозрачно, к краю — плотнее. Оболочка вместо шара
                float rim = 1.0 - saturate(abs(dot(normalize(i.n), normalize(i.view))));
                rim = pow(rim, _Rim);
                return fixed4(_Color.rgb, _Color.a * rim);
            }
            ENDCG
        }
    }
}
