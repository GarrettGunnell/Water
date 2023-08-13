Shader "Custom/FFTWater" {
		
		Properties {
			[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		}

	CGINCLUDE
        #define _TessellationEdgeLength 10
		#define NEW_LIGHTING

        struct TessellationFactors {
            float edge[3] : SV_TESSFACTOR;
            float inside : SV_INSIDETESSFACTOR;
        };

        float TessellationHeuristic(float3 cp0, float3 cp1) {
            float edgeLength = distance(cp0, cp1);
            float3 edgeCenter = (cp0 + cp1) * 0.5;
            float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);

            return edgeLength * _ScreenParams.y / (_TessellationEdgeLength * (pow(viewDistance * 0.5f, 1.2f)));
        }

        bool TriangleIsBelowClipPlane(float3 p0, float3 p1, float3 p2, int planeIndex, float bias) {
            float4 plane = unity_CameraWorldClipPlanes[planeIndex];

            return dot(float4(p0, 1), plane) < bias && dot(float4(p1, 1), plane) < bias && dot(float4(p2, 1), plane) < bias;
        }

        bool cullTriangle(float3 p0, float3 p1, float3 p2, float bias) {
            return TriangleIsBelowClipPlane(p0, p1, p2, 0, bias) ||
                   TriangleIsBelowClipPlane(p0, p1, p2, 1, bias) ||
                   TriangleIsBelowClipPlane(p0, p1, p2, 2, bias) ||
                   TriangleIsBelowClipPlane(p0, p1, p2, 3, bias);
        }

		// https://gist.github.com/TheRealMJP/c83b8c0f46b63f3a88a5986f4fa982b1
		float4 SampleTextureCatmullRom(in Texture2D<float4> tex, in SamplerState linearSampler, in float2 uv, in float2 texSize) {
			float2 samplePos = uv * texSize;
			float2 texPos1 = floor(samplePos - 0.5f) + 0.5f;

			float2 f = samplePos - texPos1;

			float2 w0 = f * (-0.5f + f * (1.0f - 0.5f * f));
			float2 w1 = 1.0f + f * f * (-2.5f + 1.5f * f);
			float2 w2 = f * (0.5f + f * (2.0f - 1.5f * f));
			float2 w3 = f * f * (-0.5f + 0.5f * f);

			float2 w12 = w1 + w2;
			float2 offset12 = w2 / (w1 + w2);

			float2 texPos0 = texPos1 - 1;
			float2 texPos3 = texPos1 + 2;
			float2 texPos12 = texPos1 + offset12;

			texPos0 /= texSize;
			texPos3 /= texSize;
			texPos12 /= texSize;

			float4 result = 0.0f;
			result += tex.Sample(linearSampler, float2(texPos0.x, texPos0.y)) * w0.x * w0.y;
			result += tex.Sample(linearSampler, float2(texPos12.x, texPos0.y)) * w12.x * w0.y;
			result += tex.Sample(linearSampler, float2(texPos3.x, texPos0.y)) * w3.x * w0.y;

			result += tex.Sample(linearSampler, float2(texPos0.x, texPos12.y)) * w0.x * w12.y;
			result += tex.Sample(linearSampler, float2(texPos12.x, texPos12.y)) * w12.x * w12.y;
			result += tex.Sample(linearSampler, float2(texPos3.x, texPos12.y)) * w3.x * w12.y;

			result += tex.Sample(linearSampler, float2(texPos0.x, texPos3.y)) * w0.x * w3.y;
			result += tex.Sample(linearSampler, float2(texPos12.x, texPos3.y)) * w12.x * w3.y;
			result += tex.Sample(linearSampler, float2(texPos3.x, texPos3.y)) * w3.x * w3.y;

			return result;
		}
    ENDCG

	SubShader {
		Tags {
			"LightMode" = "ForwardBase"
		}

		Pass {

			CGPROGRAM



			#pragma vertex dummyvp
			#pragma hull hp
			#pragma domain dp 
			#pragma geometry gp
			#pragma fragment fp


			#include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

			struct TessellationControlPoint {
                float4 vertex : INTERNALTESSPOS;
                float2 uv : TEXCOORD0;
            };

			struct VertexData {
				float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
			};

			struct v2g {
				float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
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
            Texture2D _DisplacementTex, _NormalTex, _MomentTex;
            SamplerState point_repeat_sampler, linear_repeat_sampler, trilinear_repeat_sampler;
			Texture2DArray _SpectrumTextures;

            float _Tile;

			TessellationControlPoint dummyvp(VertexData v) {
				TessellationControlPoint p;
				p.vertex = v.vertex;
				p.uv = v.uv;

				return p;
			}

			v2g vp(VertexData v) {
				v2g g;
                g.worldPos = mul(unity_ObjectToWorld, v.vertex);
				float3 displacement = _DisplacementTex.SampleLevel(linear_repeat_sampler, v.uv * _Tile, 0).rgb * 2;

				v.vertex.xyz += mul(unity_WorldToObject, displacement.xyz);
                g.pos = UnityObjectToClipPos(v.vertex);
                g.uv = v.uv;
				
				return g;
			}

			struct g2f {
				v2g data;
				float2 barycentricCoordinates : TEXCOORD9;
			};

			TessellationFactors PatchFunction(InputPatch<TessellationControlPoint, 3> patch) {
                float3 p0 = mul(unity_ObjectToWorld, patch[0].vertex);
                float3 p1 = mul(unity_ObjectToWorld, patch[1].vertex);
                float3 p2 = mul(unity_ObjectToWorld, patch[2].vertex);

                TessellationFactors f;
                float bias = -0.5 * 20;
                if (cullTriangle(p0, p1, p2, bias)) {
                    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 0;
                } else {
                    f.edge[0] = TessellationHeuristic(p1, p2);
                    f.edge[1] = TessellationHeuristic(p2, p0);
                    f.edge[2] = TessellationHeuristic(p0, p1);
                    f.inside = (TessellationHeuristic(p1, p2) +
                                TessellationHeuristic(p2, p0) +
                                TessellationHeuristic(p1, p2)) * (1 / 3.0);
                }
                return f;
            }

            [UNITY_domain("tri")]
            [UNITY_outputcontrolpoints(3)]
            [UNITY_outputtopology("triangle_cw")]
            [UNITY_partitioning("integer")]
            [UNITY_patchconstantfunc("PatchFunction")]
            TessellationControlPoint hp(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OUTPUTCONTROLPOINTID) {
                return patch[id];
            }

            [maxvertexcount(3)]
            void gp(triangle v2g g[3], inout TriangleStream<g2f> stream) {
                g2f g0, g1, g2;
                g0.data = g[0];
                g1.data = g[1];
                g2.data = g[2];

                g0.barycentricCoordinates = float2(1, 0);
                g1.barycentricCoordinates = float2(0, 1);
                g2.barycentricCoordinates = float2(0, 0);

                stream.Append(g0);
                stream.Append(g1);
                stream.Append(g2);
            }

            #define DP_INTERPOLATE(fieldName) data.fieldName = \
                data.fieldName = patch[0].fieldName * barycentricCoordinates.x + \
                                 patch[1].fieldName * barycentricCoordinates.y + \
                                 patch[2].fieldName * barycentricCoordinates.z;               

            [UNITY_domain("tri")]
            v2g dp(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DOMAINLOCATION) {
                VertexData data;
                DP_INTERPOLATE(vertex)
                DP_INTERPOLATE(uv)

                return vp(data);
            }

			#define ax 0.13f
			#define ay 0.13f

			float Beckmann(float ndoth, float roughness) {
				float exp_arg = (ndoth * ndoth - 1) / (roughness * roughness * ndoth * ndoth);

				return exp(exp_arg) / (PI * roughness * roughness * ndoth * ndoth * ndoth * ndoth);
			}

			float SchlickFresnel(float3 normal, float3 viewDir) {
				// 0.02f comes from the reflectivity bias of water kinda idk it's from a paper somewhere i'm not gonna link it tho lmaooo
				return 0.02f + (1 - 0.02f) * (pow(1 - DotClamped(normal, viewDir), 5.0f));
			}

			float SmithMaskingBeckmann(float3 H, float3 S, float roughness) {
				float hdots = max(0.001f, DotClamped(H, S));
				float a = hdots / (roughness * sqrt(1 - hdots * hdots));
				float a2 = a * a;

				return a < 1.6f ? (1.0f - 1.259f * a + 0.396f * a2) / (3.535f * a + 2.181 * a2) : 0.0f;
			}

			float SmithMaskingGGX(float3 H, float3 S, float roughness) {
				float hdots = max(0.001f, DotClamped(H, S));

				float a = hdots / (roughness * sqrt(1 - hdots * hdots));

				return rcp(1.0f + (0.5f * (-1.0f + sqrt(1.0f + rcp(a * a)))));
			}

			float4 fp(g2f f) : SV_TARGET {
                float3 lightDir = -normalize(_SunDirection);
                float3 viewDir = normalize(_WorldSpaceCameraPos - f.data.worldPos);
                float3 halfwayDir = normalize(lightDir + viewDir);

				float depth = Linear01Depth(f.data.pos.z / f.data.pos.w);
				float LdotH = DotClamped(lightDir, halfwayDir);
				float VdotH = DotClamped(viewDir, halfwayDir);

				float4 slopesFoamJacobian = _NormalTex.Sample(trilinear_repeat_sampler, f.data.uv * _Tile);

				float4 asd = SampleTextureCatmullRom(_NormalTex, trilinear_repeat_sampler, f.data.uv * _Tile, 1024);
				//float4 idk = _SpectrumTextures.Sample(point_repeat_sampler, float3(f.data.uv * _Tile, 1));
				
				// I say slopes here but the slope is also the first order moment aka the expected value
				float2 slopes = slopesFoamJacobian.xy;
				slopes *= _NormalStrength;
				float foam = lerp(slopesFoamJacobian.z, 0.0f, depth * depth);

				#ifdef NEW_LIGHTING
				float3 macroNormal = float3(0, 1, 0);
				float3 mesoNormal = normalize(float3(-slopes.x, 1.0f, -slopes.y));

				float NdotL = DotClamped(mesoNormal, lightDir);

				
				float a = 0.2f + foam;
				float ndoth = max(0.0001f, dot(mesoNormal, halfwayDir));

				float viewMask = SmithMaskingBeckmann(mesoNormal, viewDir, a);
				float lightMask = SmithMaskingBeckmann(mesoNormal, lightDir, a);
				
				float G = rcp(1 + viewMask + lightMask);

				float F = SchlickFresnel(mesoNormal, viewDir);

				float3 specular = _SpecularReflectance * F * G * Beckmann(ndoth, a);
				specular /= 4.0f * max(0.001f, DotClamped(macroNormal, lightDir));
				specular *= NdotL;

				float3 envReflection = texCUBE(_EnvironmentMap, reflect(-viewDir, mesoNormal)).rgb;

				float H = max(0.0f, _DisplacementTex.Sample(trilinear_repeat_sampler, f.data.uv * _Tile).y);
				float3 scatterColor = _Ambient;
				float3 bubbleColor = _FresnelColor;
				float bubbleDensity = 0.5f;

				
				float k1 = 1.0f * H * pow(DotClamped(lightDir, -viewDir), 4.0f) * pow(0.5f - 0.5f * dot(lightDir, mesoNormal), 3.0f);
				float k2 = 1.0f * pow(DotClamped(viewDir, mesoNormal), 2.0f);
				float k3 = 1.0f * NdotL;
				float k4 = 1.0f * bubbleDensity;

				float3 scatter = (k1 + k2) * scatterColor * _SpecularReflectance * rcp(1 + lightMask);
				scatter += k3 * scatterColor * _SpecularReflectance + k4 * bubbleColor * _SpecularReflectance;

				
				float3 output = (1 - F) * scatter + specular + F * envReflection;
				output = lerp(output, _TipColor, saturate(foam));
				
				#else
				slopes *= _NormalStrength;
				float3 normal = normalize(float3(-slopes.x, 1.0f, -slopes.y));
                normal = normalize(UnityObjectToWorldNormal(normalize(normal)));

				// normal = centralDifferenceNormal(f.data.worldPos, 0.01f);

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
				

				float3 output = _Ambient + diffuse + specular + fresnel;
				output = lerp(output, _TipColor, saturate(foam));
				#endif
				
				return float4(output, 1.0f);
			}

			ENDCG
		}
	}
}