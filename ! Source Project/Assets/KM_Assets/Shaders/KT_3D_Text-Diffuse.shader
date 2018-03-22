Shader "GUI/KT 3D Text Diffuse" {
    Properties {
        _MainTex ("Font Texture", 2D) = "white" {}
    }

    SubShader {
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType"="Plane" }
		LOD 150

		CGPROGRAM
		#pragma surface surf Lambert alpha:fade

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float4 color: Color;
		};

		void surf(Input IN, inout SurfaceOutput o) {
			half4 c = tex2D(_MainTex, IN.uv_MainTex);
			o.Albedo = IN.color.rgb;
			o.Alpha = c.a * IN.color.a;
			o.Normal = float3(0, 0, -1);
		}
		ENDCG
    }

	Fallback "GUI/KT 3D Text"
}