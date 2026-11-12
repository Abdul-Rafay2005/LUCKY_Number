# Review regression harness

These files are test-only tools. Keep them outside the production Assets tree. Copy PhaseTwoDriver.cs to Assets/Scripts and PhaseTwoEditor.cs to Assets/Scripts/Editor in an isolated copy of the Unity project. Sync the current production Assets, Packages and ProjectSettings into that copy first. Use Unity 6000.3.23f1.

Build the isolated Windows player with Unity batch arguments:

```text
-batchmode -nographics -quit -projectPath "<isolated-project>" -executeMethod PhaseTwoEditor.Build -phase2BuildPath "<output>/LuckyNumber.exe" -logFile "<build-log>"
```

Then run:

```powershell
./Tools/Validation/Run-ReviewRegression.ps1 -PlayerPath '<output>/LuckyNumber.exe' -OutputDirectory '<new-empty-output-directory>' -Port 17841
```

The harness launches three to six hidden localhost players and closes them on completion or failure. Pass `-Scenario Authority`, `Session`, `Departure`, `Spectator`, `Budget`, or `UI` to run an individual matrix. Use a fresh output directory for each run. The default `Review` scenario checks anonymous shift payload and text, runoffs, random ejection, a winning reveal followed by results, owner-only views, and rejection of a cached turn stamp after rehosting.

The scenarios use production RPCs and UI callbacks, with host-only fixtures to seed roles, shorten timers, and advance turns or ballots. Headless UI assertions cover control wiring and state hydration. Rendered layout, Relay, and cross-machine checks are separate.

To capture match screens, run `-Scenario UI -Render -Width 1280 -Height 720`. The graphics-enabled player saves offscreen PNGs into the scenario output directory. Use a fresh output directory and port for each size.

The existing Edit Mode tests remain under Assets/Tests/EditMode. Historical validation results and current outcomes are in docs/validation.md and progression.md.

Use `-Scenario Relay` to test real Unity Authentication, Lobby room codes, and Relay transport with three peers. The scenario uses the configured Unity project, creates a temporary online lobby, and leaves it at the end. It exercises cloud services from one machine.

For Phase 5, use the same isolated project with `-executeMethod PhaseTwoEditor.BuildPresentation` to apply the presentation migration before building. Run `UI -Render` for the complete three-player interaction flow. Run `-Scenario Presentation -Render` to check modal focus and click blocking, the settings preview, an error message, eight players with long names, long history, discussion, and a full voting grid. The capacity scenario renders one peer and runs the other seven headless. Use 1280×720, 1920×1080, and 1024×768 with distinct ports and output directories.
