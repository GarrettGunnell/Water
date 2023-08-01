using System;
using System.Collections;
using System.Collections.Generic;
using static System.Runtime.InteropServices.Marshal;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FFTWater : MonoBehaviour {
    public Shader waterShader;

    public Atmosphere atmosphere;

    public int planeLength = 10;
    public int quadRes = 10;

    private Camera cam;

    private Material waterMaterial;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] normals;
    
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
    public float fresnelNormalStrength = 1;

    [ColorUsageAttribute(false, true)]
    public Color tipColor;
    [Range(0.0f, 5.0f)]
    public float tipAttenuation = 1.0f;

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


    void OnEnable() {
        CreateWaterPlane();
        CreateMaterial();
        cam = GameObject.Find("Main Camera").GetComponent<Camera>();
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
        waterMaterial.SetFloat("_FresnelNormalStrength", fresnelNormalStrength);
        waterMaterial.SetFloat("_SpecularNormalStrength", specularNormalStrength);
        waterMaterial.SetInt("_UseEnvironmentMap", useTextureForFresnel ? 1 : 0);

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
