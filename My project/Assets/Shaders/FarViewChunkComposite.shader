Shader "Hidden/ProceduralEnvironment/FarViewChunkComposite"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _BackgroundTex ("Background", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BackgroundTex;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, i.uv);
                fixed4 background = tex2D(_BackgroundTex, i.uv);

                fixed outA = scene.a + (background.a * (1.0 - scene.a));
                if (outA <= 0.0)
                    return fixed4(0, 0, 0, 0);

                fixed3 outRgb = scene.rgb * scene.a;
                outRgb += background.rgb * background.a * (1.0 - scene.a);
                outRgb /= outA;
                return fixed4(outRgb, outA);
            }
            ENDCG
        }
    }

    Fallback Off
}
