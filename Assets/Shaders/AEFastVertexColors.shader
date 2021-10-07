// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "AE/Fast Vertex Colors RGB" {
	
	Properties {
		
	}
	
	
	Subshader {
		
		Tags {
			"Queue" = "Geometry"
			"RenderType" = "Opaque"
		}
		
		ZWrite On
		Fog { Mode Off }
		
		
		Pass {
			
			Lighting Off
			
			CGPROGRAM
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it does not contain a surface program or both vertex and fragment programs.
			#pragma exclude_renderers gles
			#pragma vertex vert
			
			struct appdata_color {
			    float4 vertex    : POSITION;
			    fixed4 color     : COLOR;
			};
			
			struct v2f {
				float4 pos       : SV_POSITION;
				float4 color     : COLOR;
			};
			
			v2f vert(appdata_color v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos (v.vertex);
				o.color = v.color;
				o.color.a = 1;
				return o;
			}
			
			
			ENDCG
			
		} // Pass
	
	} // SubShader

}