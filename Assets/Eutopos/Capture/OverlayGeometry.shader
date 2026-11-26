Shader "Eutopos/Capture Overlay"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct VertexInput { float4 position : POSITION; fixed4 color : COLOR; };
            struct FragmentInput { float4 position : SV_POSITION; fixed4 color : COLOR; float size : PSIZE; };

            FragmentInput Vert(VertexInput input)
            {
                FragmentInput output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.size = 3;
                return output;
            }

            fixed4 Frag(FragmentInput input) : SV_Target { return input.color; }
            ENDCG
        }
    }
}
