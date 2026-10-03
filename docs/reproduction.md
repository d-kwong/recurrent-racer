# Setup and reproduction

The tested platform is **macOS arm64**, **Python 3.10.12**, **Unity 6000.6.3f1 (45d8eee7de74)** and CPU execution. Core packages: PyTorch 2.2.2, NumPy 1.23.5, ML-Agents Python environment API 1.1.0, protobuf 3.20.3, gRPC 1.48.2, TensorBoard 2.14.0. Unity packages include ML-Agents 4.0.3, URP 17.6.0 and Input System 1.20.0; registry resolution is recorded in `Packages/manifest.json` and `packages-lock.json`.

## Python

Use an existing Python 3.10.12 installation. This repository does not install Python or Unity for you. From the repository root:

```sh
python3.10 -m venv .venv
.venv/bin/python -m pip install setuptools==70.3.0 wheel==0.48.0
# Native gRPC build on macOS arm64 needs Xcode Command Line Tools.
GRPC_PYTHON_BUILD_SYSTEM_ZLIB=1 GRPC_PYTHON_BUILD_EXT_COMPILER_JOBS=4 \
  .venv/bin/python -m pip wheel --no-build-isolation --no-deps \
  grpcio==1.48.2 --wheel-dir runs/wheels
.venv/bin/python -m pip install --find-links runs/wheels -r requirements.txt
.venv/bin/python -m pip check
.venv/bin/python -m unittest discover -s python -p test_sac.py -v
```

The historical arm64 setup successfully built this gRPC version using system zlib. Its Python API metadata constrains gRPC to ≤1.48.2, while TensorBoard requires ≥1.48.2. The local wheel itself is not redistributed. `requirements.txt` pins the installed dependency closure needed by runtime/tests/plotting; it omits the older trainer framework and unused packages. This exact trimmed install has **not** been exercised in a new virtual environment, so package resolver/build issues remain possible. No clean-machine reproducibility claim is made.

## Unity player

Open `RacingEnvironment` with Unity 6000.6.3f1. Allow the package registry imports and compilation to finish. The completed source scene is `Assets/Racing/Scenes/RacingTraining.unity`. Use **Racing → Build macOS Curriculum Player**. Do not regenerate the prototype or training scene to watch the completed environment.

A tested batch build entrypoint is also included. Set `UNITY_EDITOR` to your installed executable:

```sh
"$UNITY_EDITOR" -batchmode -quit \
  -projectPath "$PWD/RacingEnvironment" \
  -executeMethod Racing.Editor.PortfolioBuild.Build \
  -logFile "$PWD/runs/unity-build.log"
```

Create `runs/` first if using that log location. The output is `RacingEnvironment/Builds/macOS/RacingCurriculum.app`. The source scene and matching `.meta` files, procedural mesh/material assets, URP settings, Unity project settings and registry package specifications are included. Generated Library, build outputs and IDE data are ignored.

## Playback and evaluation

```sh
.venv/bin/python python/train_sac.py view \
  --checkpoint policies/selected.pt --track-mode fixed --eval-episodes 1
.venv/bin/python python/train_sac.py evaluate \
  --checkpoint policies/selected.pt --track-mode fixed --approaches --eval-episodes 3 \
  --seed 7 --output runs/selected-evaluation
```

Default paths derive from the repository location. Run from its root for the relative checkpoint/config examples. `--env` accepts another compatible player path, `--port` selects the local ML-Agents base port and `--output` selects a new run directory. Each output is created without overwriting previous runs. Stop training before starting playback, or choose a different free port.

## Training

```sh
.venv/bin/python python/train_sac.py train \
  --config configs/train.json --track-mode fixed --output runs/my-sac
```

See [training](training.md) for warm-start, from-scratch, resume and TensorBoard details. The training JSON is a defaults file; explicit flags override it. Simulator time scale is 20 during headless training/evaluation and 1 during viewing. Initialization is included, but the historical full replay/resume state is excluded, so exact mid-run continuation of the historical training trajectory is not available publicly.

## Other platforms and limits

The checked build entrypoint targets macOS. Linux/Windows players may be built from the same scene using Unity's build settings and passed with `--env`, but those platforms, package installs and numerical results have not been tested here. Unity licensing, required platform modules, registry/network access and Python build tools are external setup prerequisites. No precompiled demo build is committed.

Validation rebuilt the source copy and ran it with the existing verified Python environment, then checked local imports, commands, actor parity, evaluation, rendered playback and a small training/resume smoke. It did not install all dependencies on a fresh machine or reproduce sustained training from scratch.
