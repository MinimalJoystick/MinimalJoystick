# Code signing policy — proposed, not active

MinimalJoystick is not currently enrolled in SignPath Foundation. Do not add a "free signing provided by" attribution or a SignPath logo as a present fact before acceptance.

Proposed release trust chain:
1. Review source and dependency provenance; protect release branch/tags and require maintainer MFA.
2. Build from the public commit in CI. Keep PR jobs read-only with no signing credentials.
3. Run automated and clean-Windows acceptance tests; publish accurate pending/failed gates.
4. Obtain an approved embedded signature for the driver DLL and app/helpers.
5. Generate the driver catalog from the FINAL signed DLL and INF. Sign that catalog; verify catalog membership and signatures. Never change DLL/INF after catalog generation.
6. Package/sign the installer, record source/artifact SHA-256 and provenance, and require named human release approval.
7. Publish only after installer/upgrade/uninstall and stable identity tests pass. Driver identity must not vary with physical transport.

App/driver signing eligibility and support are independent gates. No Foundation acceptance, Microsoft certification, Windows Update inclusion or zero-prompt installation is claimed.

Privacy: current local simulator does not intentionally upload telemetry or controls. Public release will disclose any provider/update download behavior. CI/signing submission necessarily shares source-derived binaries/provenance with the selected signing service after authorization. Private keys remain with the signing provider.

Authors, reviewers, approvers and public privacy/contact pages: pending maintainer decision. There is no signing token, organization slug or application ID to configure until accepted.
