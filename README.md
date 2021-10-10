# AmbientOcclusionBaking

[![GitHub Stars](https://img.shields.io/github/stars/wingstone/AmbientOcclusionBaking.svg)](https://github.com/wingstone/AmbientOcclusionBaking)
[![GitHub Issues](https://img.shields.io/github/issues/wingstone/AmbientOcclusionBaking.svg)](https://github.com/IgorAntun/node-chat/issues)

This is a unity AO baking tool. You can bake AO using same uv layout with lightmap. After baking, you will get the baking AO textures in separate files.

## BackGround

Unity don't have a seperate AO baking tool.

The builtin baking tools bake AO into lightmaps, so you can't control ao seperately.

In addition, the  AO Effect isn't perfect visually.

I develope this tool to solve this problem.

## Features

- Soft rasterization in cpu
- Ray Tracing in cpu, using unity api
- Support simple AO and Physical AO
- Fix the seam problem in uv layout
- Simple blur AO Effect

## Usage

Open this project in Unity, Unity version is 2019.4.17f1.

In project, open AO baking window in **Window/AOBakeWindow**.

And then, it's time for you!

![Example](Images/Example.jpg?raw=true)

## Reference

1. [Unity-Vertex-AmbientOcclusion](https://github.com/kimsama/Unity-Vertex-AmbientOcclusion)
2. [Software Rasterization Algorithms for Filling Triangles](http://www.sunshine2k.de/coding/java/TriangleRasterization/TriangleRasterization.html)
3. [Interpolating in a Triangle](https://codeplea.com/triangular-interpolation)
4. [calculate-uv-coordinates-of-3d-point-on-plane-of-m.html](https://answers.unity.com/questions/383804/calculate-uv-coordinates-of-3d-point-on-plane-of-m.html)

## License

This project is licensed under the terms of the **MIT** license.
