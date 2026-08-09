Shader "TD/EnemyBodyRepair"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RepairMode ("Repair Mode", Float) = 0
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment RepairFrag
            #pragma target 2.0
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _RepairMode;

            fixed Circle(float2 samplePoint, float2 center, float radius, float feather)
            {
                return 1.0 - smoothstep(radius - feather, radius + feather, distance(samplePoint, center));
            }

            fixed3 EmberLeechBody(float2 uv)
            {
                float2 p = uv - float2(0.5, 0.507);
                float radius = length(p / float2(0.119, 0.104));
                fixed edge = smoothstep(0.64, 0.98, radius);
                fixed ridge = 1.0 - smoothstep(0.010, 0.026, abs(p.y));
                fixed core = Circle(p / float2(0.88, 1.0), float2(0.0, 0.0), 0.048, 0.012);
                fixed pupil = Circle(p, float2(0.0, 0.0), 0.020, 0.010);
                fixed3 body = lerp(fixed3(0.025, 0.085, 0.055), fixed3(0.015, 0.025, 0.022), edge);
                body = lerp(body, fixed3(0.09, 0.25, 0.10), ridge * 0.58);
                body = lerp(body, fixed3(0.25, 0.72, 0.16), core);
                return lerp(body, fixed3(0.72, 1.0, 0.30), pupil);
            }

            fixed3 FurnaceBody(float2 uv)
            {
                float2 p = uv - float2(0.5, 0.534);
                fixed verticalShade = saturate(0.46 + (p.y * 1.35));
                fixed3 body = lerp(fixed3(0.075, 0.018, 0.012), fixed3(0.34, 0.065, 0.022), verticalShade);
                fixed centerRidge = 1.0 - smoothstep(0.012, 0.034, abs(p.x));
                fixed sideSeams = 1.0 - smoothstep(0.008, 0.018, abs(abs(p.x) - 0.125));
                fixed upperSeam = 1.0 - smoothstep(0.008, 0.020, abs(p.y - 0.105));
                fixed lowerSeam = 1.0 - smoothstep(0.008, 0.020, abs(p.y + 0.105));
                fixed seam = saturate(sideSeams * 0.72 + upperSeam + lowerSeam);
                body = lerp(body, fixed3(0.018, 0.008, 0.006), seam);
                body = lerp(body, fixed3(0.56, 0.12, 0.025), centerRidge * 0.38);

                float2 corePoint = float2(p.x / 0.82, p.y);
                fixed coreOuter = Circle(corePoint, float2(0.0, -0.012), 0.105, 0.012);
                fixed coreInner = Circle(corePoint, float2(0.0, -0.012), 0.066, 0.014);
                fixed coreHot = Circle(corePoint, float2(0.0, 0.000), 0.027, 0.013);
                fixed3 coreColor = lerp(fixed3(0.72, 0.08, 0.01), fixed3(1.0, 0.48, 0.035), coreInner);
                coreColor = lerp(coreColor, fixed3(1.0, 0.91, 0.40), coreHot);
                body = lerp(body, fixed3(0.025, 0.009, 0.007), coreOuter);
                return lerp(body, coreColor, coreInner);
            }

            fixed3 CinderGliderBody(float2 uv)
            {
                float2 p = uv - float2(0.5, 0.503);
                fixed verticalShade = saturate(0.52 + p.y * 1.25);
                fixed3 body = lerp(fixed3(0.025, 0.030, 0.032), fixed3(0.13, 0.11, 0.085), verticalShade);
                fixed centerSeam = 1.0 - smoothstep(0.008, 0.020, abs(p.x));
                fixed crossSeam = 1.0 - smoothstep(0.006, 0.016, abs(p.y - 0.012));
                fixed diagonalA = 1.0 - smoothstep(0.008, 0.020, abs(p.y - abs(p.x) * 0.68 + 0.075));
                fixed seam = saturate(centerSeam * 0.46 + crossSeam * 0.72 + diagonalA * 0.62);
                body = lerp(body, fixed3(0.008, 0.010, 0.012), seam);
                fixed ventOuter = Circle(p / float2(0.78, 1.0), float2(0.0, 0.018), 0.085, 0.014);
                fixed ventInner = Circle(p / float2(0.78, 1.0), float2(0.0, 0.018), 0.048, 0.012);
                body = lerp(body, fixed3(0.24, 0.055, 0.012), ventOuter);
                body = lerp(body, fixed3(1.0, 0.43, 0.035), ventInner);
                fixed highlight = saturate((1.0 - smoothstep(0.006, 0.018, abs(p.x + 0.095))) * (1.0 - smoothstep(0.0, 0.20, abs(p.y))));
                return lerp(body, fixed3(0.53, 0.34, 0.13), highlight * 0.45);
            }

            fixed4 RepairFrag(v2f IN) : SV_Target
            {
                fixed4 raw = SampleSpriteTexture(IN.texcoord);
                fixed warmDominance = raw.r - max(raw.g, raw.b);
                fixed lowAlpha = (1.0 - smoothstep(0.26, 0.36, raw.a)) * smoothstep(0.035, 0.09, raw.a);
                fixed placeholderMask = lowAlpha * smoothstep(0.08, 0.18, warmDominance) * smoothstep(0.58, 0.76, raw.r);

                if (placeholderMask > 0.001)
                {
                    fixed3 repaired = _RepairMode < 0.5
                        ? EmberLeechBody(IN.texcoord)
                        : (_RepairMode < 1.5 ? FurnaceBody(IN.texcoord) : CinderGliderBody(IN.texcoord));
                    raw.rgb = lerp(raw.rgb, repaired, placeholderMask);
                    raw.a = max(raw.a, placeholderMask);
                }

                fixed4 color = raw * IN.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
