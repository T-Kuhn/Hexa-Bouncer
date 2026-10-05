Shader "MachineSimulator/IkSphereSurface"
{
    Properties
    {
        [HDR] _Color ("Sphere Color", Color) = (0.2, 0.55, 1, 1)
        [HDR] _RimColor ("Rim Color", Color) = (0.45, 0.85, 1, 1)
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.05
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.65
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
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
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            float4 _Color;
            float4 _RimColor;
            float _BaseAlpha;
            float _RimAlpha;
            float _RimPower;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // NOTE: abs() so back faces (Cull Off) get the same rim as front faces.
                float rim = pow(1.0 - saturate(abs(dot(normal, viewDir))), _RimPower);
                float alpha = saturate(_BaseAlpha + rim * _RimAlpha);

                return half4(lerp(_Color.rgb, _RimColor.rgb, rim), alpha * _Color.a);
            }
            ENDCG
        }
    }
}
