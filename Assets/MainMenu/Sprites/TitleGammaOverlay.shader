Shader "UI/Title Gamma Overlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Background", 2D) = "white" {}
        _OverlayRGB ("Overlay sRGB", Vector) = (0, 0.16862745, 0.25882353, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed alpha : COLOR; };
            sampler2D _MainTex;
            float4 _OverlayRGB;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.alpha = v.color.a;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 background = tex2D(_MainTex, i.uv).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                background = LinearToGammaSpace(background);
                #endif
                float3 result = lerp(background, _OverlayRGB.rgb, i.alpha);
                #ifndef UNITY_COLORSPACE_GAMMA
                result = GammaToLinearSpace(result);
                #endif
                return fixed4(result, 1);
            }
            ENDCG
        }
    }
}
