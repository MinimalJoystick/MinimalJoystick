> Historical preparation record for the initial simulated-transport snapshot. Status statements below describe preparation before publication and are retained as evidence, not current repository status. The public repository is https://github.com/MinimalJoystick/MinimalJoystick. An eligibility inquiry was submitted on 2026-11-09; the formal Foundation application remains unsent and acceptance is pending. See README.md for current publication status.

# SignPath application — UNSENT

Project: MinimalJoystick. Maintainer/repository/download URL and named reviewers/approvers: pending user choice. No affiliation or approval by SignPath is claimed.

Purpose: a Windows flight-controller companion that feeds one stable virtual joystick from USB/Bluetooth sources. Current candidate has simulated transports; actual hardware/firmware qualification is separate. Product name remains MinimalJoystick regardless of signing publisher.

Requested artifacts: app/setup/helper EXEs, a custom UMDF2 DLL and its PnP catalog (.cat). No project-owned kernel SYS is shipped. Windows supplies the kernel HID/UMDF components. Installation requires elevation and adds a root-enumerated device; removal is supported. The current installer imports a **local development certificate** and is NOT the proposed public installer. Public release must remove that development-trust flow and verify real signatures/catalog membership instead.

Important eligibility question: our UMDF DLL is derived from Microsoft's MS-PL vhidmini2 sample pinned at d49bb7d72f3651952b2b6e4ad28d14fe2247eb69. We preserve upstream notices and reproducibly generate modifications. Your conditions for modified upstream projects mention a visible fork, upstream signed release branches and code review. This project currently vendors a pinned sample, without demonstrating that signed-upstream-branch condition. Can this original custom driver built from Microsoft's sample be accepted, and what lineage/build evidence would you require? Please assess DLL and CAT coverage explicitly; generic platform support for CAT is not Foundation approval.

Current stage: local prototype, no public repository or released public installer, limited runtime evidence, latest host build blocked by Application Control. We do not yet meet your released/documented/reputation requirements and do not request an exception to your rules. Proposed path is public source with an OSI-approved license, verified CI builds, installation/uninstallation documentation and a tested release before applying.

All contributors would use MFA. Maintainer must select named author/reviewer/approver roles and manually approve each signing request. Never put signing API credentials into pull-request jobs or source control.

Policy sources checked 2026-11-09:
- https://signpath.org/terms.html
- https://docs.signpath.io/artifact-configuration/reference

No personal email, government ID, local logs, certificate private key, credentials, hardware procurement files or payments accompany this draft. Do not submit until user approves the destination/content and public repository identity.
