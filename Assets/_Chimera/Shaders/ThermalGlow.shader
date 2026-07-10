// Тепловой контур термозрения: рисуется СКВОЗЬ стены (ZTest Always), аддитивное свечение.
// Материал создаёт HeatSignature через Shader.Find — при сборке билда шейдер добавить в
// Always Included Shaders (гоча: Shader.Find-only вырезается из билда; билдов пока нет).
Shader "Chimera/ThermalGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.45, 0.12, 0.55)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZTest Always      // суть термо: видно сквозь геометрию
        ZWrite Off
        Blend SrcAlpha One // аддитивно — «светится теплом»
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
