using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;

public class Buoyancy : MonoBehaviour {
    public FFTWater waterSource;

    [Range(0.1f, 1.0f)]
    public float normalizedVoxelSize = 0.1f;

    [Range(0.0f, 1.0f)]
    public float minimumDrag = 0.0f;
    
    [Range(0.0f, 1.0f)]
    public float minimumAngularDrag = 0.05f;

    private Rigidbody rigidBody;
    private Collider cachedCollider;

    private Voxel[,,] voxels;
    private List<Vector3> receiverVoxels;
    private Vector3 voxelSize;
    private int voxelsPerAxis = 0;

    private struct Voxel {
        public Vector3 position { get; }
        private float cachedBuoyancyData;
        private FFTWater waterSource;
        public bool isReceiver;

        private AsyncGPUReadbackRequest voxelWaterRequest;

        public Voxel(Vector3 position, FFTWater waterSource, bool isReceiver) {
            this.position = position;
            this.waterSource = waterSource;
            this.cachedBuoyancyData = 0.0f;
            this.isReceiver = isReceiver;

            if (isReceiver)
                voxelWaterRequest = AsyncGPUReadback.Request(waterSource.GetBuoyancyData(), 0, 0, 1, 0, 1, 0, 1, null);
            else voxelWaterRequest = new AsyncGPUReadbackRequest();
        }

        public float GetWaterHeight() {
            return cachedBuoyancyData;
        }

        public void Update(Transform parentTransform) {
            if (isReceiver && voxelWaterRequest.done) {
                if (voxelWaterRequest.hasError) {
                    Debug.Log("Height Error detected!");
                    return;
                }

                NativeArray<ushort> buoyancyDataQuery = voxelWaterRequest.GetData<ushort>();

                cachedBuoyancyData = Mathf.HalfToFloat(buoyancyDataQuery[0]);

                Vector3 worldPos = parentTransform.TransformPoint(this.position);
                Vector2 pos = new Vector2(worldPos.x, worldPos.z);
                Vector2 uv = worldPos * waterSource.tile1;

                int x = Mathf.FloorToInt(frac(uv.x) * 1023);
                int y = Mathf.FloorToInt(frac(uv.y) * 1023);

                voxelWaterRequest = AsyncGPUReadback.Request(waterSource.GetBuoyancyData(), 0, x, 1, y, 1, 0, 1, null);
            }
        }
    };

    // Largely referenced from https://github.com/dbrizov/NaughtyWaterBuoyancy/blob/master/Assets/NaughtyWaterBuoyancy/Scripts/Core/FloatingObject.cs
    private void CreateVoxels() {
        receiverVoxels = new List<Vector3>();

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

                    voxels[x,y,z] = new Voxel(this.transform.InverseTransformPoint(point), waterSource, true);

                    if (true) receiverVoxels.Add(new Vector3(x, y, z));
                }
            }
        }
    }


    void OnEnable() {
        if (waterSource == null) return;

        rigidBody = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();

        CreateVoxels();
    }

    void Update() {
        if (waterSource == null || voxels == null) return;

        for (int i = 0; i < receiverVoxels.Count; ++i) {
            // This is probably the most ridiculous line of code I have ever written
            // but as far as I know C# doesn't let you store real references so instead
            // I store the indices of the voxels that read from the GPU that way we don't
            // waste time iterating through voxels that don't need to check their requests
            // it'd be smarter to just do a 1D array for all the voxels but I love multidimensional arrays ok
            voxels[(int)receiverVoxels[i].x, (int)receiverVoxels[i].y, (int)receiverVoxels[i].z].Update(this.transform);
        }
    }

    void FixedUpdate() {
        float volume = cachedCollider.bounds.size.x * cachedCollider.bounds.size.y * cachedCollider.bounds.size.z;
        float density = rigidBody.mass / volume;

        float submergedVolume = 0.0f;
        float voxelHeight = cachedCollider.bounds.size.y * normalizedVoxelSize;

        float UnitForce = (1.0f - density) / voxels.Length;

        for (int x = 0; x < voxelsPerAxis; ++x) {
            for (int y = 0; y < voxelsPerAxis; ++y) {
                for (int z = 0; z < voxelsPerAxis; ++z) {
                    Vector3 worldPos = this.transform.TransformPoint(voxels[x,y,z].position);

                    float waterLevel = voxels[x,y,z].isReceiver ? voxels[x,y,z].GetWaterHeight() : voxels[x,0,z].GetWaterHeight();
                    float depth = waterLevel - worldPos.y;
                    float submergedFactor = Mathf.Clamp(depth / voxelHeight, 0.0f, 1.0f);
                    submergedVolume += submergedFactor;

                    float Displacement = max(0, depth);


                    Vector3 F = -Physics.gravity * Displacement * UnitForce;
                    rigidBody.AddForceAtPosition(F, worldPos);
                }
            }
        }

        submergedVolume /= voxels.Length;

        this.rigidBody.drag = Mathf.Lerp(minimumDrag, 1.0f, submergedVolume);
        this.rigidBody.angularDrag = Mathf.Lerp(minimumAngularDrag, 1.0f, submergedVolume);
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
