Shader "Custom/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineThickness ("Outline Thickness", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineThickness;
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // Outline logic
                float tx = _OutlineThickness * _MainTex_TexelSize.x;
                float ty = _OutlineThickness * _MainTex_TexelSize.y;
                
                fixed alphaUp    = tex2D(_MainTex, IN.texcoord + float2(0, ty)).a;
                fixed alphaDown  = tex2D(_MainTex, IN.texcoord - float2(0, ty)).a;
                fixed alphaRight = tex2D(_MainTex, IN.texcoord + float2(tx, 0)).a;
                fixed alphaLeft  = tex2D(_MainTex, IN.texcoord - float2(tx, 0)).a;

                // Also check diagonals for smoother outline
                fixed alphaUR = tex2D(_MainTex, IN.texcoord + float2(tx, ty)).a;
                fixed alphaUL = tex2D(_MainTex, IN.texcoord + float2(-tx, ty)).a;
                fixed alphaDR = tex2D(_MainTex, IN.texcoord + float2(tx, -ty)).a;
                fixed alphaDL = tex2D(_MainTex, IN.texcoord + float2(-tx, -ty)).a;

                fixed outlineAlpha = max(alphaUp, max(alphaDown, max(alphaRight, alphaLeft)));
                outlineAlpha = max(outlineAlpha, max(alphaUR, max(alphaUL, max(alphaDR, alphaDL))));
                
                outlineAlpha = saturate(outlineAlpha - c.a);

                fixed4 outlineColor = _OutlineColor * outlineAlpha;
                c.rgb *= c.a; // Pre-multiply alpha for proper blending
                
                return c + outlineColor;
            }
            ENDCG
        }
    }
}
