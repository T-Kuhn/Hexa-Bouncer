Shader "MachineSimulator/IkCircleOutline"
{
    Properties
    {
        [HDR] _Color ("Circle Color", Color) = (1, 0.05, 0.05, 1)
        [HDR] _HighlightColor ("Highlight Color", Color) = (0.1, 1, 0.15, 1)
        _LineWidth ("Line Width (m)", Float) = 0.004
        _EdgeFade ("Edge Fade (m)", Float) = 0.003
        _HighlightRadius ("Highlight Radius (m)", Float) = 0.02
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        // NOTE: Below properties are driven per-renderer via MaterialPropertyBlock at runtime.
        _Center ("Circle Center (World)", Vector) = (0, 0, 0, 0)
        _Radius ("Circle Radius (m)", Float) = 0.142
        _P1 ("Solution P1 (World)", Vector) = (0, 0, 0, 0)
        _P2 ("Solution P2 (World)", Vector) = (0, 0, 0, 0)
        _HighlightsOn ("Highlights On", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            float4 _Color;
            float4 _HighlightColor;
            float4 _Center;
            float4 _P1;
            float4 _P2;
            float _LineWidth;
            float _EdgeFade;
            float _HighlightRadius;
            float _Radius;
            float _HighlightsOn;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // NOTE: Every fragment of the quad is coplanar with the circle, so the
                //       distance to the outline is an exact signed distance field.
                float d = abs(distance(i.worldPos, _Center.xyz) - _Radius);

                // NOTE: fwidth keeps the antialiasing ~1 pixel wide at any resolution/zoom.
                float aa = fwidth(d);
                float halfWidth = _LineWidth * 0.5;
                float alpha = 1.0 - smoothstep(halfWidth - aa, halfWidth + _EdgeFade + aa, d);

                // NOTE: Locally recolor the outline around both IK solutions.
                float dp = min(distance(i.worldPos, _P1.xyz), distance(i.worldPos, _P2.xyz));
                float highlight = _HighlightsOn * (1.0 - smoothstep(_HighlightRadius * 0.5, _HighlightRadius, dp));
                float3 rgb = lerp(_Color.rgb, _HighlightColor.rgb, highlight);

                return half4(rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }
}
