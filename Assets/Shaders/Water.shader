Shader "Custom/Water" {
	SubShader {
		Pass {
			Tags {
				"RenderType" = "Opaque"
                "LightMode" = "ForwardBase"
			}

			CGPROGRAM

			#pragma vertex vp
			#pragma fragment fp

			#pragma shader_feature USE_VERTEX_DISPLACEMENT

			#include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

			struct VertexData {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float3 normal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
			};

			struct Wave {
				float2 direction;
				float2 origin;
				float frequency;
				float amplitude;
				float phase;
				float steepness;
				int waveType;
			};
			
			StructuredBuffer<Wave> _Waves;

			//#define sin fastSine

			float fastSine(float x) {
				return 1.0f;
			}

			float Sine(float3 v, Wave w) {
				float2 d = w.direction;
				float xz = d.x * v.x + d.y * v.z;

				return sin(xz * w.frequency + _Time.y * w.phase) * w.amplitude;
			}

			float3 SineNormal(float3 v, Wave w) {
				float2 d = w.direction;
				float xz = d.x * v.x + d.y * v.z;

				float2 n = w.frequency * w.amplitude * d * cos(xz * w.frequency + _Time.y * w.phase);

				return float3(n.x, n.y, 0.0f);
			}

			v2f vp(VertexData v) {
				v2f i;

				#ifdef USE_VERTEX_DISPLACEMENT
					i.worldPos = mul(unity_ObjectToWorld, v.vertex);

					float h = 0.0f;
					float3 n = 0.0f;

					for (int wi = 0; wi < 4; ++wi) {
						h += Sine(i.worldPos, _Waves[wi]);
						n += SineNormal(i.worldPos, _Waves[wi]);
					}
					
					i.pos = UnityObjectToClipPos(v.vertex + float4(0.0f, h, 0.0f, 0.0f));
					i.normal = normalize(UnityObjectToWorldNormal(float3(-n.x, 1.0f, -n.y)));
				#else
					i.worldPos = mul(unity_ObjectToWorld, v.vertex);
					i.normal = normalize(UnityObjectToWorldNormal(v.normal));
					i.pos = UnityObjectToClipPos(v.vertex);
				#endif

				return i;
			}

			float4 fp(v2f i) : SV_TARGET {
                float3 lightDir = _WorldSpaceLightPos0;
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 halfwayDir = normalize(lightDir + viewDir);

                float3 ambient = float3(0.0f, 0.0f, 0.1f);
                float3 diffuse = _LightColor0.rgb * DotClamped(lightDir, normalize(i.normal)) * 0.5f + 0.5f;
				diffuse *= diffuse;
                float specular = _LightColor0.rgb * pow(DotClamped(i.normal, halfwayDir), 50.0f);


                return float4(saturate(ambient + diffuse + specular), 1.0f);
			}

			ENDCG
		}
	}
}