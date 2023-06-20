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
			#pragma shader_feature SINE_WAVE
			#pragma shader_feature STEEP_SINE_WAVE
			#pragma shader_feature GERSTNER_WAVE
			#pragma shader_feature NORMALS_IN_PIXEL_SHADER
			#pragma shader_feature CIRCULAR_WAVES

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

			#define PI 3.14159265358979323846

			float fastSine(float x) {
				return 1.0f;
			}

			float2 GetDirection(float3 v, Wave w) {
				#ifdef CIRCULAR_WAVES
                float2 p = float2(v.x, v.z);

                return normalize(p - w.origin);
				#else
				return w.direction;
				#endif
			}

			float GetWaveCoord(float3 v, float2 d, Wave w) {
				#ifdef CIRCULAR_WAVES
					float2 p = float2(v.x, v.z);
					return length(p - w.origin);
				#endif

				return v.x * d.x + v.z * d.y;
			}

			float GetTime(Wave w) {
				#ifdef CIRCULAR_WAVES
					return -_Time.y * w.phase;
				#else
				return _Time.y * w.phase;
				#endif
			}

			float Sine(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				return w.amplitude * sin(xz * w.frequency + t);
			}

			float3 SineNormal(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				float2 n = w.frequency * w.amplitude * d * cos(xz * w.frequency + t);

				return float3(n.x, n.y, 0.0f);
			}

			float SteepSine(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				return 2.0f * w.amplitude * pow((sin(xz * w.frequency + t) + 1.0f) / 2.0f, w.steepness);
			}

			float3 SteepSineNormal(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				float h = pow((sin(xz * w.frequency + t) + 1) / 2.0f, max(1.0f, w.steepness - 1));
				float2 n = d * w.steepness * w.frequency * w.amplitude * h * cos(xz * w.frequency + t);

				return float3(n.x, n.y, 0.0f);
			}

			float3 Gerstner(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				float3 g = float3(0.0f, 0.0f, 0.0f);
				g.x = w.steepness * w.amplitude * d.x * cos(w.frequency * xz + t);
				g.z = w.steepness * w.amplitude * d.y * cos(w.frequency * xz + t);
				g.y = w.amplitude * sin(w.frequency * xz + t);
				
				return g;
			}

			float3 GerstnerNormal(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				float3 n = float3(0.0f, 0.0f, 0.0f);
				
				float wa = w.frequency * w.amplitude;
				float s = sin(w.frequency * xz + _Time.y * w.phase);
				float c = cos(w.frequency * xz + _Time.y * w.phase);

				n.x = d.x * wa * c;
				n.z = d.y * wa * c;
				n.y = w.steepness * wa * s;

				return n;
			}

			float3 CalculateOffset(float3 v, Wave w) {
				#ifdef SINE_WAVE
					return float3(0.0f, Sine(v, w), 0.0f);
				#endif

				#ifdef STEEP_SINE_WAVE
					return float3(0.0f, SteepSine(v, w), 0.0f);
				#endif

				#ifdef GERSTNER_WAVE
					return Gerstner(v, w);
				#endif

				return 0.0f;
			}

			float3 CalculateNormal(float3 v, Wave w) {
				#ifdef SINE_WAVE
					return SineNormal(v, w);
				#endif

				#ifdef STEEP_SINE_WAVE
					return SteepSineNormal(v, w);
				#endif

				#ifdef GERSTNER_WAVE
					return GerstnerNormal(v, w);
				#endif

				return 0.0f;
			}

			float3 _Ambient, _DiffuseReflectance, _SpecularReflectance;
			float _Shininess;

			v2f vp(VertexData v) {
				v2f i;

				#ifdef USE_VERTEX_DISPLACEMENT
					i.worldPos = mul(unity_ObjectToWorld, v.vertex);

					float3 h = 0.0f;
					float3 n = 0.0f;

					[unroll]
					for (int wi = 0; wi < 4; ++wi) {
						h += CalculateOffset(i.worldPos, _Waves[wi]);

						#ifndef GERSTNER_WAVE
							#ifndef NORMALS_IN_PIXEL_SHADER
								n += CalculateNormal(i.worldPos, _Waves[wi]);
							#endif
						#endif
					}

					float4 newPos = v.vertex + float4(h, 0.0f);
					i.worldPos = mul(unity_ObjectToWorld, newPos);
					i.pos = UnityObjectToClipPos(newPos);
					
					#ifndef NORMALS_IN_PIXEL_SHADER
					#ifdef GERSTNER_WAVE
						[unroll]
						for (int wi = 0; wi < 4; ++wi) {
							n += CalculateNormal(i.worldPos, _Waves[wi]);
						}

                        i.normal = normalize(UnityObjectToWorldNormal(normalize(float3(-n.x, 1.0f - n.y, -n.z))));
					#else
						i.normal = normalize(UnityObjectToWorldNormal(normalize(float3(-n.x, 1.0f, -n.y))));
					#endif
					#else
						i.normal = 0.0;
					#endif
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

				float3 normal = 0.0f;

				#ifdef NORMALS_IN_PIXEL_SHADER
				[unroll]
				for (int wi = 0; wi < 4; ++wi) {
					normal += CalculateNormal(i.worldPos, _Waves[wi]);
				}

				#ifdef GERSTNER_WAVE
					normal = normalize(UnityObjectToWorldNormal(normalize(float3(-normal.x, 1.0f - normal.y, -normal.z))));
				#else
					normal = normalize(UnityObjectToWorldNormal(normalize(float3(-normal.x, 1.0f, -normal.y))));
				#endif

				#else
					normal = normalize(i.normal);
				#endif

				float ndotl = DotClamped(lightDir, normal);

				float3 diffuseReflectance = _DiffuseReflectance / PI;
                float3 diffuse = _LightColor0.rgb * ndotl * diffuseReflectance;

				float3 specularReflectance = _SpecularReflectance;
                float3 specular = _LightColor0.rgb * specularReflectance * pow(DotClamped(normal, halfwayDir), _Shininess) * ndotl;


                return float4(saturate(_Ambient + diffuse + specular), 1.0f);
			}

			ENDCG
		}
	}
}