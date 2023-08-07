Shader "Custom/FFTWater" {
		
		Properties {
			[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		}

	SubShader {
		Tags {
			"LightMode" = "ForwardBase"
		}

		Pass {

			ZWrite On

			CGPROGRAM

			#pragma vertex vp
			#pragma fragment fp

			#include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

			struct VertexData {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
			};


			#define PI 3.14159265358979323846

			float hash(uint n) {
				// integer hash copied from Hugo Elias
				n = (n << 13U) ^ n;
				n = n * (n * n * 15731U + 0x789221U) + 0x1376312589U;
				return float(n & uint(0x7fffffffU)) / float(0x7fffffff);
			}

			float3 _SunDirection, _SunColor;

			float _NormalStrength, _FresnelNormalStrength, _SpecularNormalStrength;

			samplerCUBE _EnvironmentMap;
			int _UseEnvironmentMap;

			float3 _Ambient, _DiffuseReflectance, _SpecularReflectance, _FresnelColor, _TipColor;
			float _Shininess, _FresnelBias, _FresnelStrength, _FresnelShininess, _TipAttenuation;

			float4x4 _CameraInvViewProjection;
			sampler2D _CameraDepthTexture;
            Texture2D _HeightTex, _SpectrumTex, _NormalTex;
            SamplerState point_repeat_sampler, linear_repeat_sampler;

            #define TILE 0.98

			v2f vp(VertexData v) {
				v2f i;
                i.worldPos = mul(unity_ObjectToWorld, v.vertex);
                i.normal = normalize(UnityObjectToWorldNormal(v.normal));
				float4 heightDisplacement = _HeightTex.SampleLevel(linear_repeat_sampler, v.uv * TILE + 0.01f, 0);

                i.pos = UnityObjectToClipPos(v.vertex + float3(heightDisplacement.g,  heightDisplacement.r, heightDisplacement.b));
                //i.pos = UnityObjectToClipPos(v.vertex);
                i.uv = v.uv;
				
				return i;
			}

			float3 ComputeWorldSpacePosition(float2 positionNDC, float deviceDepth) {
				float4 positionCS = float4(positionNDC * 2.0 - 1.0, deviceDepth, 1.0);
				float4 hpositionWS = mul(_CameraInvViewProjection, positionCS);
				return hpositionWS.xyz / hpositionWS.w;
			}

			float4 fp(v2f i) : SV_TARGET {
                float3 lightDir = -normalize(_SunDirection);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 halfwayDir = normalize(lightDir + viewDir);

                float height = _HeightTex.Sample(linear_repeat_sampler, i.uv * TILE + 0.01f).r;
				float3 normal = normalize(_NormalTex.SampleLevel(linear_repeat_sampler, i.uv * TILE + 0.01f, 0).rgb);
                //normal = normalize(UnityObjectToWorldNormal(normalize(normal)));

				// normal = centralDifferenceNormal(i.worldPos, 0.01f);
				normal.xz *= _NormalStrength;
				normal = normalize(normal);

				float ndotl = DotClamped(lightDir, normal);

				float3 diffuseReflectance = _DiffuseReflectance / PI;
                float3 diffuse = _LightColor0.rgb * ndotl * diffuseReflectance;

				// Schlick Fresnel
				float3 fresnelNormal = normal;
				fresnelNormal.xz *= _FresnelNormalStrength;
				fresnelNormal = normalize(fresnelNormal);
				float base = 1 - dot(viewDir, fresnelNormal);
				float exponential = pow(base, _FresnelShininess);
				float R = exponential + _FresnelBias * (1.0f - exponential);
				R *= _FresnelStrength;
				
				float3 fresnel = _FresnelColor * R;
                
				if (_UseEnvironmentMap) {
					float3 reflectedDir = reflect(-viewDir, normal);
					float3 skyCol = texCUBE(_EnvironmentMap, reflectedDir).rgb;
					float3 sun = _SunColor * pow(max(0.0f, DotClamped(reflectedDir, lightDir)), 500.0f);

					fresnel = skyCol.rgb * R;
					fresnel += sun * R;
				}


				float3 specularReflectance = _SpecularReflectance;
				float3 specNormal = normal;
				specNormal.xz *= _SpecularNormalStrength;
				specNormal = normalize(specNormal);
				float spec = pow(DotClamped(specNormal, halfwayDir), _Shininess) * ndotl;
                float3 specular = _LightColor0.rgb * specularReflectance * spec;

				// Schlick Fresnel but again for specular
				base = 1 - DotClamped(viewDir, halfwayDir);
				exponential = pow(base, 5.0f);
				R = exponential + _FresnelBias * (1.0f - exponential);

				specular *= R;
				


				float3 tipColor = _TipColor * saturate(pow(max(0.0f, height / 4.0f), _TipAttenuation));

				float3 output = _Ambient + diffuse + specular + fresnel + tipColor;
				
				//return _HeightTex.Sample(linear_repeat_sampler, i.uv * TILE + 0.01f).g * -1;
                //return _SpectrumTex.Sample(point_repeat_sampler, i.uv);
				return float4(output, 1.0f);
			}

			ENDCG
		}
	}
}