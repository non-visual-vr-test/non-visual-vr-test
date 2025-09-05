Shader "Custom/BlackOverlayShader"
{
    Properties
    {
        _Color("Main Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" }
        Pass
        {
            ZTest Always
            ColorMask RGB
            Cull Off
            Color (0, 0, 0, 1)
        }
    }
}
