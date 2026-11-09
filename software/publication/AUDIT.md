# Open-source publication audit — 2026-11-09

Historical preparation audit: the repository has since been published at https://github.com/MinimalJoystick/MinimalJoystick and its initial hosted unsigned build passed. An eligibility inquiry was submitted to SignPath; no formal Foundation application, public signing approval or signed installer release is claimed. Read preparation-status statements below in that historical context.

Scope: desktop software and its custom UMDF2 virtual joystick. No publication or signing application submitted. Owner approved MIT originals/MS-PL driver on 2026-11-09. No project-wide rights inferred for firmware, CAD, purchased models, website or reference documents.

## Evidence checked

- Microsoft sample repository/commit and all 20 vendored files recorded in `vendor/provenance.json`. Every original Git blob hash is independently compared against metadata fetched for that pinned upstream commit. Complete MS-PL text and upstream notices are retained.
- Five generated driver sources reproduce exactly from the pinned sample and project generator; source changes are attributable. The source license boundary is MS-PL, rather than MIT.
- Original project C# and own helpers/scripts form a separate MIT license boundary. No third-party controller implementation is silently bundled. Windows/.NET are external system dependencies; toolchains are build dependencies.
- Public-export allowlist includes only software source, required vendor originals, tests, schematic control art/mappings and prepublication drafts. It excludes executable binaries, certificates/private keys, installed-machine logs, project/VM toolchains, firmware, CAD and original purchased/reference material. Pattern checks are limited defense-in-depth; human review still required.
- `Build-OpenSource.ps1` uses pinned Roslyn 4.14.0, fixed assembly metadata, `/deterministic`, explicit framework references and source path mapping. See `repeat-build-results.json` for exact same-environment unsigned app/helper SHA-256 comparisons. These are not signed binaries or proof of driver/clean-room reproducibility.

## Not ready for a public signed installer

1. Owner approved MIT for original app/tools/art and MS-PL for the Microsoft sample/derivatives. Public repository/account identity still needs selection. No proprietary/commercial dual-licensing is proposed for a SignPath Foundation application.
2. SignPath's modified-upstream conditions are not demonstrated by the current vendored Microsoft sample. Platform support for .DLL and .CAT does not establish Foundation approval. Ask about the exact lineage and driver coverage before claiming free distribution.
3. Foundation requires an already released/documented, maintained project and verifiable reputation. There is no public remote configured or public release in this project yet. Do not claim affiliation or submit placeholders as real maintainer/reviewer/approver identities.
4. Current installer imports a development trust certificate. A public installer must instead verify a real signed driver package and avoid installing a developer root. Source visibility does not make that prototype trust flow production-ready.
5. Native driver binary metadata/deterministic build and CI remain unqualified; only app/helper repeatability is tested here. Proposed public CI must pin/verify dependencies and build from the public commit with no signing secrets exposed to pull requests.
6. Prototype VID/PID allocation, actual Windows runtime policy behavior, clean Windows install/upgrade/uninstall, bridge crash/recovery, stable bindings/reboot, actual transports and simulator acceptance remain open. Public signatures alone do not complete these tests.
7. No signing credentials exist and none are requested by these drafts. Signing-service account setup, provider approval, maintainer MFA and manual artifact approval remain future steps.

## Recommended order

Select license split/repository scope -> review exact allowlisted source export -> authorize publication -> qualify a clean public build and development-VM runtime tests -> obtain explicit driver-signing eligibility guidance -> apply with an honest tested release -> only then integrate approved signing and a public installer. An eligibility inquiry may be sent earlier if separately authorized; the draft is already prepared.

This audit found a feasible preparation path, not guaranteed free driver signing or a released product.
