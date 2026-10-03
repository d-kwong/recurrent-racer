# Bounded presentation pass — 2026-10-03

Ownership agreed with track generation: existing material references are reused; no shared scene or environment edits. Changes are limited to four material assets and `PortfolioPresentation`.

- Palette: muted blue-green ground, slate asphalt, warm ivory and coral curbs. Shader, opacity, rendering configuration and all material references remain unchanged.
- HUD: flat translucent navy panels, restrained cyan rule, cached text styles and a 960 × 540 reference canvas for smaller previews. The caption no longer claims every capture uses a fixed track.
- Camera setup, ray rendering/samples, capture cadence and frame timeline remain unchanged. No vehicle, collider, sensor geometry, observations, actions, reward or simulation-clock changes.

## Arcade car silhouette follow-up

User requested a low-poly car model improvement. Rendered players now attach seven small faceted mesh children under the existing `Visuals`: two angular sidepods, two wing supports, two coral endplates and a broad ivory rear wing. Flat normals and the existing coral/ivory materials preserve the arcade theme. Geometry derives dimensions from the existing body mesh; wheels and all existing visual children are untouched. The additions introduce only mesh filters/renderers and a teardown helper, with no primitive creation, colliders, Rigidbody or simulation callbacks. Runtime meshes are released on teardown. Headless and `--racing-no-render` executions exclude these details. No new assets, editor regeneration, prefab modifications or asset pipeline are required.

The HUD caption is now neutral `POLICY`, allowing honest initial-mean and SAC captures. Source validation additionally confirms the car prefab, `ArcadeVehicle.cs`, `WheelVisuals.cs` and `PrototypeSettings.asset` are byte-identical to HEAD. `git diff --check` passes. Integrated compilation, rendered silhouette review and fixed-track numerical comparison are delegated to testing/training. Acceptance: visible sidepod/rear-wing silhouette, no detached or intersecting wheel details, unchanged original component/transform state and baseline numerical behavior.

## Procedural backdrop correction

Actual procedural capture exposed a finite fixed-ground edge and blue void beneath the extended road. `PortfolioPresentation.cs` now installs a rendered-only observer that waits for a realized `ProceduralTrack.Record` after Python configuration arrives. Once available, it creates one collider-free two-triangle ground mesh over the realized left/right boundary bounds plus 120 metres on each side, reusing the existing Ground material. The plane sits at least 5 cm below both the road and existing ground top. Original Ground transform, renderer and collider are untouched. The mesh updates only when the geometry hash changes, disables in fixed mode, and releases on teardown. Headless/no-render execution does not install the observer. Fixed rendered mode creates no mesh or renderer.

Source checks: no physics APIs, primitives, collider/Rigidbody creation, timing or reward writes in the observer; original scene, car prefab, dynamics and wheel code remain byte-identical to HEAD. `git diff --check` passes. Testing owns incremental build, identical seed-1009 original-start recapture and numerical parity. Acceptance: no blue void or finite-ground cut through the procedural road view; fixed comparison capture remains valid; procedural observation/action/reward/done arrays remain identical to headless evidence.

Validation: `git diff --check` passes. A source comparison confirms that material differences are exclusively `_BaseColor` and `_Color`. Existing camera settings, sampled-ray handling, capture coroutine and capture teardown remain unchanged; installation additionally calls the render-only car detail helper. No asset pipeline or additional tests introduced.

Visual review and Unity compilation are deferred to the testing owner's integrated build. Suggested one-frame check: use `--portfolio-label "Selected SAC / fixed baseline"`, verify clear road/ground contrast, curb visibility and unobstructed HUD at 960 × 540 or above. Existing historical media is preserved and does not depict this new palette.

Handoff files:

- `RacingEnvironment/Assets/Racing/Materials/{Ground,Road,Red,White}.mat`
- `RacingEnvironment/Assets/Racing/Scripts/Presentation/PortfolioPresentation.cs`
- This document.
