# Rights and attribution

No license was found for the project's own Python/C# source, procedural assets, policy weights or historical documents. **No project license has been invented or applied.** The project is published without a general reuse license; the owner can specify different terms later. Public visibility alone does not grant a general reuse license.

The staged Unity meshes/materials are traceable to the included local procedural builders (`PrototypeBuilder` and `CarVisualBuilder`) and completed scene assets. No third-party art pack, texture library, downloaded music, branded graphic or external font file is included. This is a targeted provenance inspection, not a guarantee of ownership of original contributions; the owner should confirm rights to the project source and generated assets.

Registry packages and Python dependencies are referenced by version, not copied into the repository. Their own licenses continue to govern them. The local metadata/license inspection found:

| Dependency | Inspected license notice |
| --- | --- |
| Unity ML-Agents 4.0.3 | Apache-2.0; Unity Technologies, 2017 |
| Python ML-Agents environment API 1.1.0 | Apache-2.0 |
| PyTorch 2.2.2 | BSD-3-Clause |
| NumPy 1.23.5 | BSD-3-Clause |
| protobuf 3.20.3 | 3-Clause BSD |
| gRPC 1.48.2 | Apache-2.0 |
| TensorBoard 2.14.0 | Apache-2.0 |
| Matplotlib 3.7.5 | PSF-based license |
| Pillow 12.3.0 | MIT-CMU license |

The ML-Agents copyright/license notice is retained in [third_party/ML-Agents-LICENSE.md](third_party/ML-Agents-LICENSE.md); no ML-Agents package source is vendored. Unity Editor/Player and other Unity registry packages remain subject to their supplied terms. The rights check does not grant permission to redistribute a platform-specific Unity build; none is included.

Documentation diagrams use system sans-serif rendering; the performance SVG requests DejaVu Sans without embedding a font binary. GIF/MP4 are recordings of the included environment. CairoSVG/FFmpeg used during local media preparation are not redistributed.

Algorithm references: [SAC paper](https://arxiv.org/abs/1812.05905), [Spinning Up SAC documentation](https://spinningup.openai.com/en/latest/algorithms/sac.html). These references describe the algorithm; benchmark claims from those works are not claims about this project.
