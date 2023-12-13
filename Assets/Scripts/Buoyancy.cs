using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;

public class Buoyancy : MonoBehaviour {
    public FFTWater waterSource;

    private AsyncGPUReadbackRequest buoyancyDataRequest;

    private Rigidbody rigidBody;
    private Collider cachedCollider;
    private int cachedFrameCount;
    private Vector3 cachedBuoyancyData = Vector3.zero;
    private Vector3 cachedNormal = Vector3.zero;

    private struct Voxel {
        public Vector3 position { get; }
        private Vector3 cachedBuoyancyData;
        private Vector3 cachedNormal;
        private FFTWater waterSource;
        public bool isReceiver;

        private AsyncGPUReadbackRequest voxelWaterRequest;

        public Voxel(Vector3 position, FFTWater waterSource, bool isReceiver) {
            this.position = position;
            this.waterSource = waterSource;
            this.cachedBuoyancyData = Vector3.zero;
            this.cachedNormal = Vector3.up;
            this.isReceiver = isReceiver;

            if (isReceiver)
                voxelWaterRequest = AsyncGPUReadback.Request(waterSource.GetBuoyancyData(), 0, 0, 1, 0, 1, 0, 1, null);
            else voxelWaterRequest = new AsyncGPUReadbackRequest();
        }

        public float GetWaterHeight() {
            return cachedBuoyancyData.x;
        }

        public Vector3 GetNormal() {
            return cachedNormal;
        }

        public void Update(Transform parentTransform) {
            if (isReceiver && voxelWaterRequest.done) {
                if (voxelWaterRequest.hasError) {
                    Debug.Log("Height Error detected!");
                    return;
                }

                NativeArray<ushort> buoyancyDataQuery = voxelWaterRequest.GetData<ushort>();

                cachedBuoyancyData = new Vector3(Mathf.HalfToFloat(buoyancyDataQuery[0]), Mathf.HalfToFloat(buoyancyDataQuery[1]), Mathf.HalfToFloat(buoyancyDataQuery[2]));

                cachedNormal = new Vector3(-cachedBuoyancyData.y, 1.0f, -cachedBuoyancyData.z);
                cachedNormal.Normalize();

                Vector3 worldPos = parentTransform.TransformPoint(this.position);
                Vector2 pos = new Vector2(worldPos.x, worldPos.z);
                Vector2 uv = worldPos * waterSource.tile1;

                int x = Mathf.FloorToInt(frac(uv.x) * 1023);
                int y = Mathf.FloorToInt(frac(uv.y) * 1023);

                voxelWaterRequest = AsyncGPUReadback.Request(waterSource.GetBuoyancyData(), 0, x, 1, y, 1, 0, 1, null);
            }
        }
    };

    private Voxel[,,] voxels;
    private Vector3 voxelSize;
    private int voxelsPerAxis = 0;

    [Range(0.1f, 1.0f)]
    public float normalizedVoxelSize = 0.1f;


    // Largely referenced from https://github.com/dbrizov/NaughtyWaterBuoyancy/blob/master/Assets/NaughtyWaterBuoyancy/Scripts/Core/FloatingObject.cs
    private void CreateVoxels() {
        Quaternion initialRotation = this.transform.rotation;
        this.transform.rotation = Quaternion.identity;

        Bounds bounds = this.cachedCollider.bounds;
        voxelSize = bounds.size * normalizedVoxelSize;
        voxelsPerAxis = Mathf.RoundToInt(1.0f / normalizedVoxelSize);
        voxels = new Voxel[voxelsPerAxis, voxelsPerAxis, voxelsPerAxis];

        for (int x = 0; x < voxelsPerAxis; ++x) {
            for (int y = 0; y < voxelsPerAxis; ++y) {
                for (int z = 0; z < voxelsPerAxis; ++z) {
                    Vector3 point = voxelSize;
                    point.Scale(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
                    point += bounds.min;

                    voxels[x,y,z] = new Voxel(this.transform.InverseTransformPoint(point), waterSource, y == 0);
                }
            }
        }
    }


    void OnEnable() {
        if (waterSource == null) return;

        rigidBody = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();

        buoyancyDataRequest = AsyncGPUReadback.Request(waterSource.GetBuoyancyData(), 0, 0, 1, 0, 1, 0, 1, null);
        cachedFrameCount = Time.frameCount;

        CreateVoxels();
    }

    void Update() {
        if (waterSource == null || voxels == null) return;

        for (int x = 0; x < voxelsPerAxis; ++x) {
            for (int y = 0; y < voxelsPerAxis; ++y) {
                for (int z = 0; z < voxelsPerAxis; ++z) {
                    voxels[x,y,z].Update(this.transform);
                }
            }
        }
    }

    void FixedUpdate() {
        Vector3 pos = this.transform.position;

        float volume = cachedCollider.bounds.size.x * cachedCollider.bounds.size.y * cachedCollider.bounds.size.z;
        float density = rigidBody.mass / volume;

        Vector3 maxBuoyancy = 1.0f * volume * -Physics.gravity;

        Vector3 maxVoxelForce = maxBuoyancy / voxels.Length;
        float submergedVolume = 0.0f;
        float voxelHeight = cachedCollider.bounds.size.y * normalizedVoxelSize;

        for (int x = 0; x < voxelsPerAxis; ++x) {
            for (int y = 0; y < voxelsPerAxis; ++y) {
                for (int z = 0; z < voxelsPerAxis; ++z) {
                    Vector3 worldPos = this.transform.TransformPoint(voxels[x,y,z].position);

                    float waterLevel = voxels[x,y,z].isReceiver ? voxels[x,y,z].GetWaterHeight() : voxels[x,0,z].GetWaterHeight();
                    float depth = waterLevel - worldPos.y;
                    float submergedFactor = Mathf.Clamp(depth / voxelHeight, 0.0f, 1.0f);
                    submergedVolume += submergedFactor;

                    Vector3 surfaceNormal = voxels[x,y,z].isReceiver ? voxels[x,y,z].GetNormal() : voxels[x,0,z].GetNormal();
                    Quaternion surfaceRotation = Quaternion.FromToRotation(new Vector3(0, 1, 0), surfaceNormal);
                    surfaceRotation = Quaternion.Slerp(surfaceRotation, Quaternion.identity, depth);

                    Vector3 F = surfaceRotation * (maxVoxelForce * submergedFactor);
                    rigidBody.AddForceAtPosition(F, worldPos);
                }
            }
        }

        submergedVolume /= voxels.Length;

        this.rigidBody.drag = Mathf.Lerp(0.0f, 1.0f, submergedVolume);
        this.rigidBody.angularDrag = Mathf.Lerp(0.5f, 1.0f, submergedVolume);
    }

    void OnDisable() {
        voxels = null;
    }

    private void OnDrawGizmos() {
        if (this.voxels != null) {        
            for (int x = 0; x < voxelsPerAxis; ++x) {
                for (int y = 0; y < voxelsPerAxis; ++y) {
                    for (int z = 0; z < voxelsPerAxis; ++z) {
                        Gizmos.color = this.voxels[x,y,z].isReceiver ? Color.green : Color.red;
                        Gizmos.DrawCube(this.transform.TransformPoint(this.voxels[x,y,z].position), this.voxelSize * 0.8f);
                    }
                }
            }
        }
    }
}
