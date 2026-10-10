Shader "MachineSimulator/CameraImagePlane"
{
    Properties
    {
        _MainTex ("Camera Texture (BGR)", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            // NOTE: Two-sided, so the image plane is readable from the camera side as well as from the ball side.
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                // NOTE: UVCCameraPlugin uploads the plugin's BGR24 frame as RGB24, so swap R and B (same as Custom/BGR2RGB).
                return fixed4(col.b, col.g, col.r, 1);
            }
            ENDCG
        }
    }
}
