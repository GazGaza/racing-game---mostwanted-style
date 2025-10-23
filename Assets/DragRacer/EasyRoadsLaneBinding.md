# Manqana EasyRoads Lane Binding

1. **Place the car on an EasyRoads road segment**  
   - Open the road network prefab (typically `ER Road Network`) and locate the modular road object that matches your track.  
   - Drag the `Manqana` prefab so it becomes a child (or nested descendant) of that road in the hierarchy. The controller can auto-detect the nearest `ERModularRoad` in its parents when the *Auto Assign Road* toggle is enabled.

2. **Verify lane settings on the road**  
   - In the EasyRoads inspector select the road object and confirm *Lanes > Total Lanes* reflects the number of driveable lanes you want.  
   - Adjust *Lane Width* if needed; the controller reads this automatically. If the road lacks lane data, set the controller's **Fallback Lane Count** and **Lane Width Override** fields to match your layout.

3. **Tune the controller**  
   - With the car selected, expand the **EasyRoads Binding** header on `HybridSplineCarController`.  
   - Ensure **Auto Assign Road** is on (or drag a specific `ERModularRoad` into the slot).  
   - For wider or narrower roads, enter a **Lane Width Override** value in metres.  
   - Use **Lane Snap Strength** and **Align Strength** to balance how tightly the chassis hugs each lane versus allowing slight drift for wheel physics.

4. **Lane change controls**  
   - At runtime press `A` or `D` to request the adjacent lane. The controller blends the current lane index toward the requested one and samples EasyRoads spline data each fixed update so the car glides smoothly between centers.

5. **Troubleshooting**  
   - If the car does not move with the road, press Play and check the console. Missing spline data (from unbuilt roads) will be reported. Click the EasyRoads toolbar *Build* button to regenerate spline samples, then re-enter Play Mode.  
   - Make sure the `Manqana` prefab's Rigidbody is not kinematic; the lane helper drives it with `MovePosition` so WheelColliders remain reactive.
