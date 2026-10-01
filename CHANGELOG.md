# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.2.0] - 2026-10-01

### Added
- **Create Crowd Setup** (`Tools → VAT → Create Crowd Setup`, or right-click a `VATClipData` → *VAT → Create Crowd Setup*): builds the material, character prefab, `VATRenderer`, and a spawner in a new or existing SubScene from one bake. Reuses existing assets for the same bake, and can add a camera, light and ground to an empty scene.
- *VAT → Quick Crowd Setup (Defaults)* context action that skips the window.
- "Set Up Crowd For This Bake" button in the baker window after a successful bake.

### Changed
- Baker menu moved to `Tools → VAT → Baker`.
- `VATBaker.Bake` now returns the saved `VATClipData` (or null on invalid input).

## [0.1.0] - 2026-10-01

### Added
- VAT Baker editor window (`Tools → VAT Baker`) that bakes a SkinnedMeshRenderer and four clips (Idle, Walk, Run, Attack) into a position texture, a UV2 mesh and a `VATClipData` asset.
- Rebakes overwrite existing assets in place, preserving GUIDs and references.
- `Custom/VAT` URP shader with two-frame interpolation, shadow casting, and support for non-instanced variants.
- `VATDebugRenderer` for previewing a single baked character without ECS.
- ECS runtime: `VATCharacterAuthoring`, `AgentAuthoring`, `SpawnerAuthoring`, `SpawnSystem`, `WanderSystem`, `VATAnimationSystem`.
- `VATRenderer`, drawing entities per character in instanced batches of up to 1,023.
