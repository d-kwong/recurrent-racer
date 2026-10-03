# Arcade presentation handoff

Owned changes: `Assets/Racing/Materials/Ground.mat` and `Assets/Racing/Scripts/Presentation/PortfolioPresentation.cs` only. No scene/prefab regeneration or new asset pipeline.

- Bright green Ground material feeds both fixed ground and the existing collider-free procedural extension. Asphalt and coral/ivory curbs retain contrast.
- Rendered car replaces only the three original body/stripe/cockpit MeshRenderer appearances with flat-shaded formula fuselage, stepped slender nose/stripe, cockpit, front wing/endplates, sidepods and elevated rear wing. Original transforms, wheel positions, animation, collider and Rigidbody remain intact. Meshes reuse existing materials and are released on teardown.
- Directional lighting uses soft shadows with 0.7 strength; no light transform or simulation settings change.
- `--portfolio-camera chase|overview` defaults to chase. Overview disables only chase rendering control and observes the completed route after Python configuration. Its camera stays fixed while the car moves; geometry hash changes trigger reframe. Fixed mode uses the existing centerline plus road/curb width. Procedural mode uses realized boundaries. Pitch 62 degrees, yaw 0, perspective FOV 48 degrees, aspect-aware fit to all eight AABB corners with 10% margin. `PortfolioOverview.FitDistance` and `Corners` are public pure helpers for editor verification.
- `camera.csv` records geometry, mode, transform, FOV/aspect, route bounds and margin for overview captures. The camera helper reads no car state, physics, actions, rewards or clock. Chase capture uses existing frame/tick timeline.
- HUD uses a narrow 38-pixel title strip, best-fit policy label and compact 36-pixel metrics strip on a width-scaled reference canvas. Sensor legend appears only for ray captures.

Final visual review accepted green grass without exposed backdrop edges, the formula-car silhouette with unchanged wheels, readable compact HUD and complete whole-route framing. The collider-free grass margin scales with route bounds. Capture records through runner shutdown, retaining coasting, stalls and terminal hold.

Final publication build `559b3cc5f34710ce67ab7a393a69e54b9ed230c71026e7099adec1fb49126fbe` matches all eight development traces exactly. All ten final chase/overview captures passed clean-finish and exact headless/camera/action parity checks. Unity helper validation passed nine overview aspect/bounds cases and repeated same-geometry pose, FOV and far-clip restoration.

Headless/no-render exits before visual installation. Final source audit confirms both scenes, Car prefab, ArcadeVehicle, WheelVisuals and PrototypeSettings are unchanged from HEAD. `git diff --check` passes.

Compile/reset correction: Unity IMGUI uses explicit `GUIStyle.CalcSize` font fitting (20 down to 12). Overview reapplies its cached pose, 48-degree FOV and far clip each rendered LateUpdate, protecting identical-geometry resets from `ChaseCamera.Snap()`. Recalculation and metadata remain geometry-change-only. Compilation and reset checks passed.
