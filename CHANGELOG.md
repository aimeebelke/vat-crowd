# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-10-01

### Added
- VAT Baker editor window (`Tools → VAT Baker`) that bakes a SkinnedMeshRenderer and four clips (Idle, Walk, Run, Attack) into a position texture, a UV2 mesh and a `VATClipData` asset.
- Rebakes overwrite existing assets in place, preserving GUIDs and references.
- `Custom/VAT` URP shader with two-frame interpolation, shadow casting, and support for non-instanced variants.
- `VATDebugRenderer` for previewing a single baked character without ECS.
- ECS runtime: `VATCharacterAuthoring`, `AgentAuthoring`, `SpawnerAuthoring`, `SpawnSystem`, `WanderSystem`, `VATAnimationSystem`.
- `VATRenderer`, drawing entities per character in instanced batches of up to 1,023.
