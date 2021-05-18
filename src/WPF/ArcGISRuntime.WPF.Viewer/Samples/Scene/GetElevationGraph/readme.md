# Get elevation graph

Get the elevation for a given point on a surface in a scene.

![Image of get elevation at point](GetElevationGraph.jpg)

## Use case

Finding the chnage in elevation between two gieven points. 

## How to use the sample

Tap on any two points on the surface to generate a map of the elevation difference.

## How it works

1. Create a `SceneView` and `Scene` with an imagery base map.
2. Set an `ArcGISTiledElevationSource` as the elevation source of the scene's base surface.
3. Use the `GetElevationAsync(surfacePoint)` method on the base surface to asynchronously get the elevation.

## Relevant API

* ArcGISTiledElevationSource
* BaseSurface
* ElevationSourcesList
* SceneView

## Additional information

`GetElevationAsync(surfacePoint)` retrieves the most accurate available elevation value at a given point. To do this, the method must go to the server or local raster file and load the highest level of detail of data for the target location and return the elevation value.

If multiple elevation sources are present in the surface, the top most visible elevation source with a valid elevation in the given location is used to determine the result.

## Tags

elevation, point, surface