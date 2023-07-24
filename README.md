# Water Rendering

by Garrett Gunnell

This repo contains the code associated with my [video](https://youtu.be/PH9q0HNBjT4) on the same subject.

## Features

* Sum Of Sines Fluid Simulation (from GPU Gems)
* * Sine Wave
* * Exponential Sine Wave
* * Gerstner Wave
* FBM Fluid Simulation
* * Euler Wave (idk that's what I'm calling it)
* Analytical Normals For Both
* Basic Atmosphere Shader
* * Distance Fog w/ Height Attenuation
* * Sun
* * Simple Skybox Animation
* Basic PBR Water Shader
* * Blinn Phong
* * Fresnel Reflectance
* * Optional Cubemap Reflections

These shaders **do not** contain tessellation passes and therefore aren't optimized or intended to be used as a production ready asset. Please use as a reference for your own shaders.

## Examples

![example1](./Examples/example.png)

## References

https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models

https://iquilezles.org/articles/fbm/

https://www.shadertoy.com/view/MdXyzX

http://filmicworlds.com/blog/everything-has-fresnel/