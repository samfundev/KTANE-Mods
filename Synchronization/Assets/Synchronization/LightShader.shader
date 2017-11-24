// Combination of Mobile/Diffuse and LightingLambert, but added in support
// for blending with a second unlit texture.

// Mobile/Diffuse:
// Simplified Diffuse shader. Differences from regular Diffuse one:
// - no Main Color

Shader "Custom/Light" {
Properties {
    _Blend ("Blend", Range (0,1)) = 0
	_LitColor ("Lit Color", Color) = (1,1,1,1)
    _UnlitColor ("Unlit Color", Color) = (1,1,1,1)
}
SubShader {
	Tags { "RenderType"="Opaque" }
	LOD 150

CGPROGRAM
// Mobile improvement: noforwardadd
// http://answers.unity3d.com/questions/1200437/how-to-make-a-conditional-pragma-surface-noforward.html
// http://gamedev.stackexchange.com/questions/123669/unity-surface-shader-conditinally-noforwardadd
//#pragma surface surf BlendLitUnlit
#pragma surface surf Lambert

half _Blend;
fixed4 _LitColor;
fixed4 _UnlitColor;

struct Input {
	float2 uv_MainTex;
};

struct SurfaceOutputCustom {
	half3 Albedo;
	half3 Normal;
	half3 Emission;
	half Specular;
	half Gloss;
	half Alpha;
	// Custom fields:
	half3 Unlit;
};

void surf(Input IN, inout SurfaceOutput o) {
	o.Albedo = _UnlitColor.rgb;
	o.Emission = lerp((0, 0, 0, 0), _LitColor.rgb, _Blend);
	o.Alpha = _LitColor.a;
}
/*
void surf(Input IN, inout SurfaceOutputCustom o) {
	o.Albedo = _MainColor.rgb;
	o.Alpha = _MainColor.a;
	o.Unlit = _UnlitColor.rgb;
}*/
/*
half4 LightingBlendLitUnlit(SurfaceOutputCustom s, half3 lightDir, half atten)
{
	fixed diff = max(0, dot(s.Normal, lightDir));

	fixed4 c;
	c.rgb = lerp(s.Unlit * _LightColor0.rgb * (diff * atten), s.Albedo, _Blend);
	c.a = s.Alpha;

	return c;
}*/

ENDCG
}

Fallback "Mobile/Diffuse"
}
