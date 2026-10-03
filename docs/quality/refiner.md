# Quality showcase refiner handoff

The parent accepted the quality gate and the README now showcases procedural driving. Historical fixed-track and pilot content is preserved in `docs/history.md`; no old policies, results or media changed.

Measured evidence: 8 / 8 development routes clean; 40 / 40 untouched test routes clean, mean 16.03 s actual simulated time; zero additional training. All five geometry-preselected routes passed in both cameras with exact headless/camera numerical parity and bitwise actor-export actions. Final publication build repeats those ten clean captures with exact parity. `docs/quality/showcase.md` distinguishes frozen test-build and final presentation-build provenance.

Media: ten complete contiguous recorded first-episode MP4s and ten uniform 1–9 s GIF excerpts. Sidecars record policy/build/seed/camera, acquisition gap, last-frame-to-terminal gap, terminal holds, reset trim count and SHA256 of MP4/GIF/timestamp CSV. Raw frames remain untouched. All media decode and hashes verify. FFmpeg was a temporary local dependency and is not published.

Refiner performed no Git/GitHub, policy, environment or testing mutations. Operator receives exact file inventory in `docs/quality/refiner-files.json` and concrete title/body in `docs/quality/pr-draft.md`.
