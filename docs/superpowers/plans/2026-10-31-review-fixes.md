# Recruitment review fixes implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Native implementation, one final independent review.

**Goal:** Implement the approved acceptance criteria of GitHub issues #17–#21.

**Architecture:** Keep the existing GameComponent save format compatible. Add a capacity-aware start destination selector, measured applicant row layout, durable per-applicant decisions with ownership recovery, and bounded value-only history. Existing gameplay and unrelated planned issues #13–#16 remain outside this change.

**Tech Stack:** C#, .NET SDK 10.0.403, RimWorld 1.7 reference assemblies, xUnit, Python XML checker, GitHub Actions.

**Spec:** GitHub issues https://github.com/pengin0503/Rimworld-Recruitment/issues/17 through /21, approved in the conversation.

## Global Constraints

- Preserve existing saves and defNames; keep Harmony requirements unchanged.
- Keep 0–3 applicant rolls secret before arrival.
- History stores only values and strings, never Pawn references; retain the latest 100 records.
- Regenerate and commit the release DLL using the pinned SDK.

## Review Focus

- Partial failure on the second of three applicants must preserve all unprocessed applicants across save/load.
- A pawn successfully spawned or placed in a transport container must not be delivered twice after an exception.
- A selected map that disappears or fills its recruitment slots must reject before charging.
- Long descriptions and changed width/UI scale must recompute row heights without shrinking portraits into unusable controls.
- Old saves lack history and decision-progress fields; defaults must keep old decisions and letters usable.

### Task 1: Capacity-aware envoy destination (#17)

**Files:** Logic/DestinationPicker.cs, RecruitmentUtility.cs, GameComponent_Recruitment.cs, WorldObjectComp_RecruitmentRequest.cs, destination dialog, translations, DestinationPickerTests.cs.

- [x] Add failing tests: a full populous home is skipped, all-full returns null, arrival selection ignores start capacity.
- [x] Add optional capacity to MapCandidate; start selection filters slots, arrival keeps existing fallback behavior.
- [x] Offer a destination menu before caravan route/request; save the chosen map in arrival action and revalidate before payment.
- [x] Run destination tests and full suite; commit.

### Task 2: Applicant layout (#18)

**Files:** Dialog_Applicants.cs, Logic/ApplicantLayout.cs, layout tests.

- [x] Add failing tests: measured long description determines row height, short rows retain minimum height.
- [x] Cache measurements by width and UI scale; accumulate row offsets and use matching draw widths/fonts.
- [x] Run layout tests and full suite; commit.

### Task 3: Ownership-preserving decisions (#19)

**Files:** GameComponent_Recruitment.cs, partial transfer implementation, ApplicantGroup.cs, Logic/TransferAttempt.cs, transfer tests.

- [x] Add failing tests with preparation/transfer exceptions, external ownership after partial success, recovery and retry.
- [x] Persist accepted choices and counters before processing; retain unfinished applicants in the original group/container and retry every 250 ticks.
- [x] Own generated applicants before changing factions in both join modes; remove group and letter only after every pawn is settled.
- [x] Normalize, change faction and transfer inside one recovery boundary; never repeat a committed delivery.
- [x] Run transfer tests, full suite and production build; commit.

### Task 4: Bounded history (#20)

**Files:** RecruitmentHistoryEntry.cs, history partial implementation, GameComponent_Recruitment.cs, ApplicantGroup.cs, RecruitmentDrive.cs, MainTabWindow_Recruitment.cs, Logic/BoundedHistory.cs, translations, history tests.

- [x] Add failing tests for replacement on retry and retention of the newest 100 entries.
- [x] Save value snapshots for method/origin/destination, cost, times, arrived/accepted/rejected/expired counts and outcome codes.
- [x] Record zero applicants, generation failures, lost destination, transfer recovery and completion without leaking active rolls.
- [x] Add scrollable history to the recruitment tab with localized results and timing.
- [x] Run history tests, full suite, XML checks and build; commit.

### Task 5: Release verification and delivery (#21)

**Files:** .github/workflows/release.yml, docs/manual-test.md, README.md, release DLL.

- [x] Require dotnet test before packaging/releasing.
- [x] Document multi-colony choices, long descriptions, error recovery, save/load and history verification.
- [x] Run full test suite, build, deterministic DLL repeat build and XML checker.
- [x] Perform independent whole-branch review, fix material findings, push branch and open a non-draft PR with Closes #17–#21.


## Verification and decisions

- Native implementation on isolated checkout/branch `fix/recruitment-review-17-21`; scope #17–#21 only.
- Clean baseline: 58 tests passed. Added regression coverage for destinations, row geometry, transfer failure boundaries, and history retention/update.
- Independent whole-branch review found one material preparation/transfer boundary bug; a new regression test reproduced it, then passed after gating commit detection on transfer having begun.
- Old group snapshots mark unavailable start times/costs explicitly unknown rather than inventing zeros.
- In-game rendering, save/load reference resolution, and modded worker fault injection remain manual checks; see docs/manual-test.md. No game runtime was launched.
