using System;
using System.Collections;
using System.Collections.Generic;
using static System.Runtime.InteropServices.Marshal;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FFTWater : MonoBehaviour {
    public Shader waterShader;
    public ComputeShader fftComputeShader;

    public Atmosphere atmosphere;

    public int planeLength = 10;
    public int quadRes = 10;

    private Camera cam;

    private Material waterMaterial;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] normals;

    [Header("Spectrum Settings")]
    [Range(0, 2048)]
    public int lengthScale = 256;

    [Range(0, 100000)]
    public int seed = 0;

    [Range(0.0f, 100.0f)]
    public float A = 1.0f;

    public Vector2 wind = new Vector2(1.0f, 1.0f);
    
    [Range(0.0f, 200.0f)]
    public float windSpeed = 1.0f;

    [Range(0.0f, 20.0f)]
    public float gravity = 9.81f;

    [Range(0.0f, 1000.0f)]
    public float damping = 1.0f;

    [Range(0.0f, 200.0f)]
    public float repeatTime = 200.0f;

    public bool updateSpectrum = false;

    [Range(0.0f, 5.0f)]
    public float speed = 1.0f;

    public Vector2 lambda = new Vector2(-1.0f, -1.0f);

    public bool useFFT = true;


    [Header("Material Settings")]
    [Range(0.0f, 5.0f)]
    public float normalStrength = 1;

    // Shader Settings
    [ColorUsageAttribute(false, true)]
    public Color ambient;

    [ColorUsageAttribute(false, true)]
    public Color diffuseReflectance;

    [ColorUsageAttribute(false, true)]
    public Color specularReflectance;

    [Range(0.0f, 10.0f)]
    public float shininess = 1.0f;

    [Range(0.0f, 5.0f)]
    public float specularNormalStrength = 1.0f;

    [ColorUsageAttribute(false, true)]
    public Color fresnelColor;

    public bool useTextureForFresnel = false;
    public Texture environmentTexture;

    [Range(0.0f, 1.0f)]
    public float fresnelBias = 0.0f;

    [Range(0.0f, 3.0f)]
    public float fresnelStrength = 1.0f;

    [Range(0.0f, 20.0f)]
    public float fresnelShininess = 5.0f;

    [Range(0.0f, 5.0f)]
    public float fresnelNormalStrength = 1.0f;

    [ColorUsageAttribute(false, true)]
    public Color tipColor;

    [Range(0.0f, 5.0f)]
    public float tipAttenuation = 1.0f;

    private RenderTexture heightTex, normalTex, initialSpectrumTex, progressedSpectrumTex, twiddleFactorTex, pingPongTex, htildeTex, htildeSlopeXTex, htildeSlopeZTex;
    private int N, logN, threadGroupsX, threadGroupsY;

    public RenderTexture GetDisplacementMap() {
        return heightTex;
    }

    public RenderTexture GetNormalMap() {
        return normalTex;
    }

    public RenderTexture GetInitialSpectrum() {
        return initialSpectrumTex;
    }

    public RenderTexture GetProgressedSpectrum() {
        return progressedSpectrumTex;
    }

    public RenderTexture GetTwiddleFactor() {
        return twiddleFactorTex;
    }

    private void CreateWaterPlane() {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Water";
        mesh.indexFormat = IndexFormat.UInt32;

        float halfLength = planeLength * 0.5f;
        int sideVertCount = planeLength * quadRes;

        vertices = new Vector3[(sideVertCount + 1) * (sideVertCount + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        for (int i = 0, x = 0; x <= sideVertCount; ++x) {
            for (int z = 0; z <= sideVertCount; ++z, ++i) {
                vertices[i] = new Vector3(((float)x / sideVertCount * planeLength) - halfLength, 0, ((float)z / sideVertCount * planeLength) - halfLength);
                uv[i] = new Vector2((float)x / sideVertCount, (float)z / sideVertCount);
                tangents[i] = tangent;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.tangents = tangents;

        int[] triangles = new int[sideVertCount * sideVertCount * 6];

        for (int ti = 0, vi = 0, x = 0; x < sideVertCount; ++vi, ++x) {
            for (int z = 0; z < sideVertCount; ti += 6, ++vi, ++z) {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + 1;
                triangles[ti + 2] = vi + sideVertCount + 2;
                triangles[ti + 3] = vi;
                triangles[ti + 4] = vi + sideVertCount + 2;
                triangles[ti + 5] = vi + sideVertCount + 1;
            }
        }

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        normals = mesh.normals;
    }

    void CreateMaterial() {
        if (waterShader == null) return;
        if (waterMaterial != null) return;

        waterMaterial = new Material(waterShader);

        MeshRenderer renderer = GetComponent<MeshRenderer>();

        renderer.material = waterMaterial;
    }

    void SetFFTUniforms() {
        Vector2 windDirection = wind;
        windDirection.Normalize();
        fftComputeShader.SetFloat("_A", A / 100000000);
        fftComputeShader.SetVector("_Wind", windDirection * windSpeed);
        fftComputeShader.SetVector("_Lambda", lambda);
        fftComputeShader.SetFloat("_FrameTime", Time.time * speed);
        fftComputeShader.SetFloat("_Gravity", gravity);
        fftComputeShader.SetFloat("_RepeatTime", repeatTime);
        fftComputeShader.SetFloat("_Damping", damping / 100000);
        fftComputeShader.SetInt("_N", N);
        fftComputeShader.SetInt("_Seed", seed);
        fftComputeShader.SetInt("_LengthScale", lengthScale);
    }

    void InverseFFT(RenderTexture spectrumTex) {
        bool pingPong = false;

        fftComputeShader.SetTexture(5, "_PrecomputedData", twiddleFactorTex);
        fftComputeShader.SetTexture(5, "_Buffer0", spectrumTex);
        fftComputeShader.SetTexture(5, "_Buffer1", pingPongTex);
        for (int i = 0; i < logN; ++i) {
            pingPong = !pingPong;
            fftComputeShader.SetInt("_Step", i);
            fftComputeShader.SetBool("_PingPong", pingPong);
            fftComputeShader.Dispatch(5, threadGroupsX, threadGroupsY, 1);
        }

        fftComputeShader.SetTexture(6, "_PrecomputedData", twiddleFactorTex);
        fftComputeShader.SetTexture(6, "_Buffer0", spectrumTex);
        fftComputeShader.SetTexture(6, "_Buffer1", pingPongTex);
        for (int i = 0; i < logN; ++i) {
            pingPong = !pingPong;
            fftComputeShader.SetInt("_Step", i);
            fftComputeShader.SetBool("_PingPong", pingPong);
            fftComputeShader.Dispatch(6, threadGroupsX, threadGroupsY, 1);
        }

        if (pingPong) Graphics.Blit(pingPongTex, spectrumTex);

        fftComputeShader.SetTexture(7, "_Buffer0", spectrumTex);
        fftComputeShader.Dispatch(7, threadGroupsX, threadGroupsY, 1);
    }

    RenderTexture CreateRenderTex(int width, int height, RenderTextureFormat format) {
        RenderTexture rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
        rt.enableRandomWrite = true;
        rt.Create();

        return rt;
    }


    void OnEnable() {
        CreateWaterPlane();
        CreateMaterial();
        cam = GameObject.Find("Main Camera").GetComponent<Camera>();

        N = 256;
        threadGroupsX = Mathf.CeilToInt(N / 8.0f);
        threadGroupsY = Mathf.CeilToInt(N / 8.0f);

        initialSpectrumTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf);
        progressedSpectrumTex = CreateRenderTex(N, N, RenderTextureFormat.RGHalf);
        htildeTex = CreateRenderTex(N, N, RenderTextureFormat.RGHalf);
        htildeSlopeXTex = CreateRenderTex(N, N, RenderTextureFormat.RGHalf);
        htildeSlopeZTex = CreateRenderTex(N, N, RenderTextureFormat.RGHalf);
        pingPongTex = CreateRenderTex(N, N, RenderTextureFormat.RGHalf);
        heightTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf);
        normalTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf);

        SetFFTUniforms();
        fftComputeShader.SetTexture(0, "_HeightTex", heightTex);
        fftComputeShader.SetTexture(0, "_NormalTex", normalTex);
        fftComputeShader.SetTexture(0, "_InitialSpectrumTex", initialSpectrumTex);
        fftComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        //Precompute Twiddle Factors for FFT
        logN = (int)Mathf.Log(N, 2);
        twiddleFactorTex = CreateRenderTex(logN, N, RenderTextureFormat.ARGBHalf);

        fftComputeShader.SetTexture(4, "_PrecomputeBuffer", twiddleFactorTex);
        fftComputeShader.Dispatch(4, logN, (N / 2) / 8, 1);

    }

    void Update() {
        waterMaterial.SetVector("_Ambient", ambient);
        waterMaterial.SetVector("_DiffuseReflectance", diffuseReflectance);
        waterMaterial.SetVector("_SpecularReflectance", specularReflectance);
        waterMaterial.SetVector("_TipColor", tipColor);
        waterMaterial.SetVector("_FresnelColor", fresnelColor);
        waterMaterial.SetFloat("_Shininess", shininess * 100);
        waterMaterial.SetFloat("_FresnelBias", fresnelBias);
        waterMaterial.SetFloat("_FresnelStrength", fresnelStrength);
        waterMaterial.SetFloat("_FresnelShininess", fresnelShininess);
        waterMaterial.SetFloat("_TipAttenuation", tipAttenuation);
        waterMaterial.SetFloat("_NormalStrength", normalStrength);
        waterMaterial.SetFloat("_FresnelNormalStrength", fresnelNormalStrength);
        waterMaterial.SetFloat("_SpecularNormalStrength", specularNormalStrength);
        waterMaterial.SetInt("_UseEnvironmentMap", useTextureForFresnel ? 1 : 0);

        SetFFTUniforms();
        if (updateSpectrum) {
            fftComputeShader.SetTexture(0, "_HeightTex", heightTex);
            fftComputeShader.SetTexture(0, "_NormalTex", normalTex);
            fftComputeShader.SetTexture(0, "_InitialSpectrumTex", initialSpectrumTex);
            fftComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        }
        
        if (useFFT) {
            // Progress Spectrum For FFT
            fftComputeShader.SetTexture(3, "_InitialSpectrumTex", initialSpectrumTex);
            fftComputeShader.SetTexture(3, "_HTilde", htildeTex);
            fftComputeShader.Dispatch(3, threadGroupsX, threadGroupsY, 1);

            // Compute FFT For Height
            InverseFFT(htildeTex);
            Graphics.Blit(htildeTex, heightTex);
        } else {
            // Progress Spectrum For DFT
            fftComputeShader.SetTexture(1, "_InitialSpectrumTex", initialSpectrumTex);
            fftComputeShader.SetTexture(1, "_ProgressedSpectrumTex", progressedSpectrumTex);
            fftComputeShader.Dispatch(1, threadGroupsX, threadGroupsY, 1);

            // Calculate DFT and store height, displacement, and normal from DFT output into textures
            fftComputeShader.SetTexture(2, "_HeightTex", heightTex);
            fftComputeShader.SetTexture(2, "_ProgressedSpectrumTex", progressedSpectrumTex);
            fftComputeShader.SetTexture(2, "_NormalTex", normalTex);
            fftComputeShader.Dispatch(2, threadGroupsX, threadGroupsY, 1);
        }
        waterMaterial.SetTexture("_HeightTex", heightTex);
        waterMaterial.SetTexture("_NormalTex", normalTex);
        waterMaterial.SetTexture("_SpectrumTex", progressedSpectrumTex);

        if (useTextureForFresnel) {
            waterMaterial.SetTexture("_EnvironmentMap", environmentTexture);
        }

        if (atmosphere != null) {
            waterMaterial.SetVector("_SunDirection", atmosphere.GetSunDirection());
            waterMaterial.SetVector("_SunColor", atmosphere.GetSunColor());
        }

        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;
        waterMaterial.SetMatrix("_CameraInvViewProjection", viewProjMatrix.inverse);
    }

    void OnDisable() {
        if (waterMaterial != null) {
            Destroy(waterMaterial);
            waterMaterial = null;
        }

        if (mesh != null) {
            Destroy(mesh);
            mesh = null;
            vertices = null;
            normals = null;
        }

        Destroy(heightTex);
        Destroy(normalTex);
        Destroy(initialSpectrumTex);
        Destroy(progressedSpectrumTex);
    }

    private void OnDrawGizmos() {
        /*
        if (vertices == null) return;

        for (int i = 0; i < vertices.Length; ++i) {
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(transform.TransformPoint(displacedVertices[i]), 0.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.TransformPoint(displacedVertices[i]), displacedNormals[i]);
        }
        */
    }
}
