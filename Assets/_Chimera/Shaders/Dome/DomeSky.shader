// Купол-павильон: небо одним Unlit-шейдером (s10c).
// Градиент и облака КВАНТОВАНЫ (banding как стиль лоу-поли, вердикт художника),
// диски светил — плоские, без glow. Туман URP сюда не применяется осознанно.
// Переключение день/ночь — uniform _DayT, а не два материала.
Shader "Chimera/DomeSky"
{
    Properties
    {
        _DayT ("Day factor", Range(0, 1)) = 1
        _Center ("Dome center", Vector) = (0, -700, 0, 0)
        _SunDir ("Sun dir", Vector) = (0, 1, 0, 0)
        _MoonDir ("Moon dir", Vector) = (0, -1, 0, 0)
        _DayTop ("Day top", Color) = (0.35, 0.55, 0.75, 1)
        _DayHor ("Day horizon", Color) = (0.78, 0.84, 0.86, 1)
        _NightTop ("Night top", Color) = (0.02, 0.03, 0.08, 1)
        _NightHor ("Night horizon", Color) = (0.08, 0.10, 0.16, 1)
        _SunColor ("Sun disc", Color) = (1, 0.95, 0.85, 1)
        _MoonColor ("Moon disc", Color) = (0.8, 0.85, 0.95, 1)
        _CloudColor ("Cloud", Color) = (1, 1, 1, 1)
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.45
        _CloudScroll ("Cloud scroll", Float) = 0.004
        _DuskT ("Dusk factor", Range(0, 1)) = 0
        _DuskColor ("Dusk band", Color) = (0.95, 0.45, 0.2, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Off
        ZWrite On
        Fog { Mode Off }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _DayT;
            float _DuskT;
            float3 _Center;
            float3 _SunDir;
            float3 _MoonDir;
            float3 _DayTop;
            float3 _DayHor;
            float3 _NightTop;
            float3 _NightHor;
            float3 _SunColor;
            float3 _MoonColor;
            float3 _CloudColor;
            float _CloudCover;
            float _CloudScroll;
            float3 _DuskColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            float Quant(float x, float steps)
            {
                return floor(saturate(x) * steps) / steps;
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // Направление ВЗГЛЯДА, не радиус купола: иначе горизонт селится на
                // зарытой кромке и пояс заката не виден (поймано кадром s10c).
                float3 dir = normalize(IN.worldPos - _WorldSpaceCameraPos);
                float h = dir.y;
                // Дневной и ночной градиенты, квант 4 ступени.
                float g = Quant(h * 0.5 + 0.5, 4.0);
                float3 day = lerp(_DayHor, _DayTop, g);
                float3 night = lerp(_NightHor, _NightTop, g);
                float3 col = lerp(night, day, _DayT);
                // Облака: 3 слоя FBM, масштаб под клочья ~10° (не пятна 30°), квант 3 ступени,
                // только днём, гаснут к горизонту (там пояс заката).
                if (h > 0.02 && _DayT > 0.01)
                {
                    float2 p = dir.xz / (h + 0.35) * 6.5 + float2(_Time.y * _CloudScroll, 0);
                    float n = VNoise(p) * 0.55 + VNoise(p * 2.7 + 13.0) * 0.3 + VNoise(p * 6.1 + 31.0) * 0.15;
                    float cl = Quant(n, 3.0);
                    float cover = step(1.0 - _CloudCover, cl) * smoothstep(0.12, 0.4, h);
                    col = lerp(col, _CloudColor, cover * _DayT * 0.85);
                }
                // Закатный пояс у горизонта. Без abs()/exp(): только проверенные
                // арифметики (в этом окружении SmoothStep уже врал — доверяем кадру).
                float duskBand = _DuskT * saturate(1.0 - h * h * 25.0);
                col = lerp(col, _DuskColor, duskBand * 0.8);
                // Плоские диски светил.
                float sun = step(0.9994, dot(dir, normalize(_SunDir)));
                float moon = step(0.99965, dot(dir, normalize(_MoonDir)));
                col += _SunColor * sun * _DayT;
                col += _MoonColor * moon * (1.0 - _DayT);
                // Ниже горизонта — тёмная дымка (кромка за кольцом, почти не видно).
                if (h < 0.0)
                    col = lerp(col, night * 0.5, saturate(-h * 6.0));
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
