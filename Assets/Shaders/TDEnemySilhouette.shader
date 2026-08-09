Shader "TD/EnemySilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
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
            #pragma fragment SilhouetteFrag
            #pragma target 2.0
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnitySprites.cginc"

            fixed4 SilhouetteFrag(v2f IN) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, IN.texcoord).a * IN.color.a;
                fixed3 rgb = IN.color.rgb * alpha;
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
