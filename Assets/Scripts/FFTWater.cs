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

    public struct SpectrumSettings {
        public float scale;
        public float angle;
        public float spreadBlend;
        public float swell;
        public float alpha;
        public float peakOmega;
        public float gamma;
        public float shortWavesFade; 
    }

    SpectrumSettings[] spectrums = new SpectrumSettings[8];

    [System.Serializable]
    public struct DisplaySpectrumSettings {
        [Range(0, 5)]
        public float scale;
        public float windSpeed;
        [Range(0.0f, 360.0f)]
        public float windDirection;
        public float fetch;
        [Range(0, 1)]
        public float spreadBlend;
        [Range(0, 1)]
        public float swell;
        public float peakEnhancement;
        public float shortWavesFade;
    }

    [Header("Spectrum Settings")]
    [Range(0, 100000)]
    public int seed = 0;

    [Range(0.0f, 0.1f)]
    public float lowCutoff = 0.0001f;

    [Range(0.1f, 9000.0f)]
    public float highCutoff = 9000.0f;

    [Range(0.0f, 20.0f)]
    public float gravity = 9.81f;

    [Range(2.0f, 20.0f)]
    public float depth = 20.0f;

    [Range(0.0f, 200.0f)]
    public float repeatTime = 200.0f;

    [Range(0.0f, 5.0f)]
    public float speed = 1.0f;

    public Vector2 lambda = new Vector2(1.0f, 1.0f);

    [Range(0.0f, 10.0f)]
    public float displacementDepthFalloff = 1.0f;

    public bool updateSpectrum = false;

    [Header("Layer One")]
    [Range(0, 2048)]
    public int lengthScale1 = 256;
    [Range(0.01f, 3.0f)]
    public float tile1 = 8.0f;
    public bool visualizeTile1 = false;
    public bool visualizeLayer1 = false;
    public bool contributeDisplacement1 = true;
    [SerializeField]
    public DisplaySpectrumSettings spectrum1;
    [SerializeField]
    public DisplaySpectrumSettings spectrum2;

    [Header("Layer Two")]
    [Range(0, 2048)]
    public int lengthScale2 = 256;
    [Range(0.01f, 3.0f)]
    public float tile2 = 8.0f;
    public bool visualizeTile2 = false;
    public bool visualizeLayer2 = false;
    public bool contributeDisplacement2 = true;
    [SerializeField]
    public DisplaySpectrumSettings spectrum3;
    [SerializeField]
    public DisplaySpectrumSettings spectrum4;

    [Header("Layer Three")]
    [Range(0, 2048)]
    public int lengthScale3 = 256;
    [Range(0.01f, 3.0f)]
    public float tile3 = 8.0f;
    public bool visualizeTile3 = false;
    public bool visualizeLayer3 = false;
    public bool contributeDisplacement3 = true;
    [SerializeField]
    public DisplaySpectrumSettings spectrum5;
    [SerializeField]
    public DisplaySpectrumSettings spectrum6;

    [Header("Layer Four")]
    [Range(0, 2048)]
    public int lengthScale4 = 256;
    [Range(0.01f, 3.0f)]
    public float tile4 = 8.0f;
    public bool visualizeTile4 = false;
    public bool visualizeLayer4 = false;
    public bool contributeDisplacement4 = true;
    [SerializeField]
    public DisplaySpectrumSettings spectrum7;
    [SerializeField]
    public DisplaySpectrumSettings spectrum8;

    [Header("Normal Settings")]
    [Range(0.0f, 20.0f)]
    public float normalStrength = 1;
    
    [Range(0.0f, 10.0f)]
    public float normalDepthFalloff = 1.0f;

    [Header("Material Settings")]
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

    [Header("PBR Settings")]
    [ColorUsageAttribute(false, true)]
    public Color sunIrradiance;

    [ColorUsageAttribute(false, true)]
    public Color scatter;

    [ColorUsageAttribute(false, true)]
    public Color bubble;

    [Range(0.0f, 1.0f)]
    public float bubbleDensity = 1.0f;

    [Range(0.0f, 2.0f)]
    public float roughness = 0.1f;

    [Range(0.0f, 2.0f)]
    public float foamRoughnessModifier = 1.0f;

    [Range(0.0f, 10.0f)]
    public float heightModifier = 1.0f;

    [Range(0.0f, 10.0f)]
    public float wavePeakScatterStrength = 1.0f;
    
    [Range(0.0f, 10.0f)]
    public float scatterStrength = 1.0f;

    [Range(0.0f, 10.0f)]
    public float scatterShadowStrength = 1.0f;

    [Range(0.0f, 2.0f)]
    public float environmentLightStrength = 1.0f;

    [Header("Foam Settings")]
    [ColorUsageAttribute(false, true)]
    public Color foam;

    [Range(-2.0f, 2.0f)]
    public float foamBias = -0.5f;

    [Range(-10.0f, 10.0f)]
    public float foamThreshold = 0.0f;

    [Range(0.0f, 1.0f)]
    public float foamAdd = 0.5f;

    [Range(0.0f, 1.0f)]
    public float foamDecayRate = 0.05f;

    [Range(0.0f, 10.0f)]
    public float foamDepthFalloff = 1.0f;

    [Range(-2.0f, 2.0f)]
    public float foamSubtract1 = 0.0f;
    [Range(-2.0f, 2.0f)]
    public float foamSubtract2 = 0.0f;
    [Range(-2.0f, 2.0f)]
    public float foamSubtract3 = 0.0f;
    [Range(-2.0f, 2.0f)]
    public float foamSubtract4 = 0.0f;

    private RenderTexture displacementTextures, 
                          slopeTextures, 
                          initialSpectrumTextures, 
                          pingPongTex, 
                          pingPongTex2, 
                          spectrumTextures,
                          buoyancyDataTex;

    private ComputeBuffer spectrumBuffer;

    private int N, logN, threadGroupsX, threadGroupsY;

    public RenderTexture GetDisplacementMap() {
        return displacementTextures;
    }

    public RenderTexture GetSlopeMap() {
        return slopeTextures;
    }

    public RenderTexture GetInitialSpectrum() {
        return initialSpectrumTextures;
    }

    public RenderTexture GetDisplacementSpectrum() {
        return spectrumTextures;
    }

    public RenderTexture GetBuoyancyData() {
        return buoyancyDataTex;
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

        waterMaterial = new Material(waterShader);

        MeshRenderer renderer = GetComponent<MeshRenderer>();

        renderer.material = waterMaterial;
    }

    void SetFFTUniforms() {
        fftComputeShader.SetVector("_Lambda", lambda);
        fftComputeShader.SetFloat("_FrameTime", Time.time * speed);
        fftComputeShader.SetFloat("_DeltaTime", Time.deltaTime);
        fftComputeShader.SetFloat("_Gravity", gravity);
        fftComputeShader.SetFloat("_RepeatTime", repeatTime);
        fftComputeShader.SetInt("_N", N);
        fftComputeShader.SetInt("_Seed", seed);
        fftComputeShader.SetInt("_LengthScale0", lengthScale1);
        fftComputeShader.SetInt("_LengthScale1", lengthScale2);
        fftComputeShader.SetInt("_LengthScale2", lengthScale3);
        fftComputeShader.SetInt("_LengthScale3", lengthScale4);
        fftComputeShader.SetFloat("_NormalStrength", normalStrength);
        fftComputeShader.SetFloat("_FoamThreshold", foamThreshold);
        fftComputeShader.SetFloat("_Depth", depth);
        fftComputeShader.SetFloat("_LowCutoff", lowCutoff);
        fftComputeShader.SetFloat("_HighCutoff", highCutoff);
        fftComputeShader.SetFloat("_FoamBias", foamBias);
        fftComputeShader.SetFloat("_FoamDecayRate", foamDecayRate);
        fftComputeShader.SetFloat("_FoamThreshold", foamThreshold);
        fftComputeShader.SetFloat("_FoamAdd", foamAdd);
    }

    float JonswapAlpha(float fetch, float windSpeed) {
        return 0.076f * Mathf.Pow(gravity * fetch / windSpeed / windSpeed, -0.22f);
    }

    float JonswapPeakFrequency(float fetch, float windSpeed) {
        return 22 * Mathf.Pow(windSpeed * fetch / gravity / gravity, -0.33f);
    }

    void FillSpectrumStruct(DisplaySpectrumSettings displaySettings, ref SpectrumSettings computeSettings) {
        computeSettings.scale = displaySettings.scale;
        computeSettings.angle = displaySettings.windDirection / 180 * Mathf.PI;
        computeSettings.spreadBlend = displaySettings.spreadBlend;
        computeSettings.swell = Mathf.Clamp(displaySettings.swell, 0.01f, 1);
        computeSettings.alpha = JonswapAlpha(displaySettings.fetch, displaySettings.windSpeed);
        computeSettings.peakOmega = JonswapPeakFrequency(displaySettings.fetch, displaySettings.windSpeed);
        computeSettings.gamma = displaySettings.peakEnhancement;
        computeSettings.shortWavesFade = displaySettings.shortWavesFade;
    }

    void SetSpectrumBuffers() {
        FillSpectrumStruct(spectrum1, ref spectrums[0]);
        FillSpectrumStruct(spectrum2, ref spectrums[1]);
        FillSpectrumStruct(spectrum3, ref spectrums[2]);
        FillSpectrumStruct(spectrum4, ref spectrums[3]);
        FillSpectrumStruct(spectrum5, ref spectrums[4]);
        FillSpectrumStruct(spectrum6, ref spectrums[5]);
        FillSpectrumStruct(spectrum7, ref spectrums[6]);
        FillSpectrumStruct(spectrum8, ref spectrums[7]);

        spectrumBuffer.SetData(spectrums);
        fftComputeShader.SetBuffer(0, "_Spectrums", spectrumBuffer);
    }

    void InverseFFT(RenderTexture spectrumTextures) {
        fftComputeShader.SetTexture(3, "_FourierTarget", spectrumTextures);
        fftComputeShader.Dispatch(3, 1, N, 1);
        fftComputeShader.SetTexture(4, "_FourierTarget", spectrumTextures);
        fftComputeShader.Dispatch(4, 1, N, 1);
    }

    RenderTexture CreateRenderTex(int width, int height, int depth, RenderTextureFormat format, bool useMips) {
        RenderTexture rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.volumeDepth = depth;
        rt.useMipMap = useMips;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 16;
        rt.Create();

        return rt;
    }

    RenderTexture CreateRenderTex(int width, int height, RenderTextureFormat format, bool useMips) {
        RenderTexture rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.useMipMap = useMips;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 16;
        rt.Create();

        return rt;
    }


    void OnEnable() {
        CreateWaterPlane();
        CreateMaterial();
        cam = GameObject.Find("Main Camera").GetComponent<Camera>();

        N = 1024;
        logN = (int)Mathf.Log(N, 2.0f);
        threadGroupsX = Mathf.CeilToInt(N / 8.0f);
        threadGroupsY = Mathf.CeilToInt(N / 8.0f);

        initialSpectrumTextures = CreateRenderTex(N, N, 4, RenderTextureFormat.ARGBHalf, true);

        // pingPongTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf, false);
        // pingPongTex2 = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf, false);
        buoyancyDataTex = CreateRenderTex(N, N, RenderTextureFormat.RHalf, false);

        displacementTextures = CreateRenderTex(N, N, 4, RenderTextureFormat.ARGBHalf, true);

        slopeTextures = CreateRenderTex(N, N, 4, RenderTextureFormat.RGHalf, true);

        spectrumTextures = CreateRenderTex(N, N, 8, RenderTextureFormat.ARGBHalf, true);

        spectrumBuffer = new ComputeBuffer(8, 8 * sizeof(float));

        SetFFTUniforms();
        SetSpectrumBuffers();
        // Compute initial JONSWAP spectrum
        fftComputeShader.SetTexture(0, "_InitialSpectrumTextures", initialSpectrumTextures);
        fftComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        fftComputeShader.SetTexture(1, "_InitialSpectrumTextures", initialSpectrumTextures);
        fftComputeShader.Dispatch(1, threadGroupsX, threadGroupsY, 1);
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
        waterMaterial.SetFloat("_NormalStrength", normalStrength);
        waterMaterial.SetFloat("_FresnelNormalStrength", fresnelNormalStrength);
        waterMaterial.SetFloat("_SpecularNormalStrength", specularNormalStrength);
        waterMaterial.SetInt("_UseEnvironmentMap", useTextureForFresnel ? 1 : 0);
        waterMaterial.SetFloat("_Tile0", tile1);
        waterMaterial.SetFloat("_Tile1", tile2);
        waterMaterial.SetFloat("_Tile2", tile3);
        waterMaterial.SetFloat("_Tile3", tile4);
        waterMaterial.SetFloat("_Roughness", roughness);
        waterMaterial.SetFloat("_FoamRoughnessModifier", foamRoughnessModifier);
        waterMaterial.SetVector("_SunIrradiance", sunIrradiance);
        waterMaterial.SetVector("_BubbleColor", bubble);
        waterMaterial.SetVector("_ScatterColor", scatter);
        waterMaterial.SetVector("_FoamColor", foam);
        waterMaterial.SetFloat("_BubbleDensity", bubbleDensity);
        waterMaterial.SetFloat("_HeightModifier", heightModifier);
        waterMaterial.SetFloat("_DisplacementDepthAttenuation", displacementDepthFalloff);
        waterMaterial.SetFloat("_NormalDepthAttenuation", normalDepthFalloff);
        waterMaterial.SetFloat("_FoamDepthAttenuation", foamDepthFalloff);
        waterMaterial.SetFloat("_WavePeakScatterStrength", wavePeakScatterStrength);
        waterMaterial.SetFloat("_ScatterStrength", scatterStrength);
        waterMaterial.SetFloat("_ScatterShadowStrength", scatterShadowStrength);
        waterMaterial.SetFloat("_EnvironmentLightStrength", environmentLightStrength);

        waterMaterial.SetInt("_DebugTile0", visualizeTile1 ? 1 : 0);
        waterMaterial.SetInt("_DebugTile1", visualizeTile2 ? 1 : 0);
        waterMaterial.SetInt("_DebugTile2", visualizeTile3 ? 1 : 0);
        waterMaterial.SetInt("_DebugTile3", visualizeTile4 ? 1 : 0);

        waterMaterial.SetInt("_DebugLayer0", visualizeLayer1 ? 1 : 0);
        waterMaterial.SetInt("_DebugLayer1", visualizeLayer2 ? 1 : 0);
        waterMaterial.SetInt("_DebugLayer2", visualizeLayer3 ? 1 : 0);
        waterMaterial.SetInt("_DebugLayer3", visualizeLayer4 ? 1 : 0);

        waterMaterial.SetInt("_ContributeDisplacement0", contributeDisplacement1 ? 1 : 0);
        waterMaterial.SetInt("_ContributeDisplacement1", contributeDisplacement2 ? 1 : 0);
        waterMaterial.SetInt("_ContributeDisplacement2", contributeDisplacement3 ? 1 : 0);
        waterMaterial.SetInt("_ContributeDisplacement3", contributeDisplacement4 ? 1 : 0);

        waterMaterial.SetFloat("_FoamSubtract0", foamSubtract1);
        waterMaterial.SetFloat("_FoamSubtract1", foamSubtract2);
        waterMaterial.SetFloat("_FoamSubtract2", foamSubtract3);
        waterMaterial.SetFloat("_FoamSubtract3", foamSubtract4);

        SetFFTUniforms();
        if (updateSpectrum) {
            SetSpectrumBuffers();
            fftComputeShader.SetTexture(0, "_InitialSpectrumTextures", initialSpectrumTextures);
            fftComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
            fftComputeShader.SetTexture(1, "_InitialSpectrumTextures", initialSpectrumTextures);
            fftComputeShader.Dispatch(1, threadGroupsX, threadGroupsY, 1);
        }
        
        // Progress Spectrum For FFT
        fftComputeShader.SetTexture(2, "_InitialSpectrumTextures", initialSpectrumTextures);
        fftComputeShader.SetTexture(2, "_SpectrumTextures", spectrumTextures);
        fftComputeShader.Dispatch(2, threadGroupsX, threadGroupsY, 1);

        // Compute FFT For Height
        InverseFFT(spectrumTextures);

        // Assemble maps
        fftComputeShader.SetTexture(5, "_DisplacementTextures", displacementTextures);
        fftComputeShader.SetTexture(5, "_SpectrumTextures", spectrumTextures);
        fftComputeShader.SetTexture(5, "_SlopeTextures", slopeTextures);
        fftComputeShader.SetTexture(5, "_BuoyancyData", buoyancyDataTex);
        fftComputeShader.Dispatch(5, threadGroupsX, threadGroupsY, 1);

        
        displacementTextures.GenerateMips();
        slopeTextures.GenerateMips();


        waterMaterial.SetTexture("_DisplacementTextures", displacementTextures);
        waterMaterial.SetTexture("_SlopeTextures", slopeTextures);

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

        Destroy(displacementTextures);
        Destroy(slopeTextures);
        Destroy(initialSpectrumTextures);
        Destroy(spectrumTextures);
        Destroy(pingPongTex);
        Destroy(pingPongTex2);

        spectrumBuffer.Dispose();
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
