Shader "Operations/Document Frame"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI texture", 2D) = "white" {}
        _OfficeTex ("Office background", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _OfficeTex;
            struct v2f { float4 vertex : SV_POSITION; float2 position : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex);
                o.position = float2(uv.x * 1850, (1 - uv.y) * 825);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Coordinates share the 1920x1080 reference layout of this frame.
                float2 q = abs(i.position - float2(925, 412.5)) - float2(890, 377.5);
                float distance = length(max(q, 0)) + min(max(q.x, q.y), 0) - 20;
                float2 uv = (i.position + float2(37, 179)) / float2(1920, 1080);
                uv.y = 1 - uv.y;
                float3 background = tex2D(_OfficeTex, uv).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                background = LinearToGammaSpace(background);
                #endif
                float3 panel = lerp(background, float3(0.1195, 0.1790, 0.2692), 0.9038);
                float inside = 1 - smoothstep(-0.5, 0.5, distance);
                float border = inside * smoothstep(-2.5, -1.5, distance);
                float glow = exp(-pow(max(distance, 0) / 7.5, 2)) * 0.45;
                float3 color = lerp(background, float3(0.36, 0.76, 1), glow);
                color = lerp(color, panel, inside);
                color = lerp(color, 1, border);
                #ifndef UNITY_COLORSPACE_GAMMA
                color = GammaToLinearSpace(color);
                #endif
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
