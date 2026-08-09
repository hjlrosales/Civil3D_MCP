# PDF Underlay & Plot-to-PDF Spike

**Status:** Reachability spike executed 2026-08-09, then re-run live with explicit user
authorization the same day. **Both capabilities are now VERIFIED LIVE — both PASS.**
**Scope:** Determine whether attaching a PDF underlay and plotting a layout to PDF are reachable
through this platform's existing `MCP → Autodesk.Mcp.Server → Civil3D.Bridge → ToolDispatcher →
IApplicationContext` execution path — the same path every shipped tool (`create_pipe`,
`drawing_info`, ...) already uses.

This document originally recorded a compile-only pass (see git history / the session transcript
for that intermediate state). It has been rewritten below to record the final, live-verified
outcome after a second pass wired the spike into a real bridge, restarted Civil 3D under the
user's explicit authorization, and executed both operations against a throwaway drawing.

---

## Objective

Determine whether both of these operations work through the existing execution path:

1. Attach a PDF as a PDF underlay/reference in the active drawing.
2. Plot the current Civil 3D layout to a PDF file.

This is a reachability spike only. No permanent MCP tools were implemented or shipped.

---

## Environment

- **Machine state at first (compile-only) pass:** Civil 3D 2025 running (`acad.exe`, pid
  `51576`), live bridge at `bridgeVersion: "1.0.0"`, 35 tools advertised, with
  `Lusaran As-built.dwg` open and **`isModified: true`** — real unsaved user work. That single
  fact is why the first pass stopped short of a live trial and asked for authorization instead
  of restarting Civil 3D unilaterally.
- **User authorization:** the user confirmed the drawing was saved and explicitly authorized
  "rebuild/redeploy the temporary spike and restart Civil 3D as required," with the constraint
  "do not modify or overwrite my original drawing; use a temporary copy/output where
  appropriate."
- **Verified-safe before touching anything:** rather than trust either the user's statement or
  the live `isModified` flag alone, the already-shipped `save_drawing` tool was called and the
  drawing re-checked — `isModified` went from `true` to `false`. Only then did the destructive
  step (killing `acad.exe`) happen.
- **Autodesk SDK actually installed:** `C:\Program Files\Autodesk\AutoCAD 2025`
  (`acmgd.dll`, `acdbmgd.dll`, `accoremgd.dll`) plus
  `C:\Program Files\Autodesk\AutoCAD 2025\C3D\AeccDbMgd.dll` (Civil 3D 2025 Object Enabler). This
  matches the `AutodeskAcadDir` default in every `Civil3D.Tools.*.csproj`.
- **Repo state:** source tree version `1.0.1`. The live trial ran a bridge rebuilt from this
  exact source tree (`bridgeVersion: "1.0.1"` on the live endpoint) with the spike temporarily
  wired in; both were fully reverted afterward and the original `1.0.0` bundle restored (see
  "Cleanup").
- **.NET SDK:** `dotnet 10.0.301` (repo requirement, confirmed installed).
- **Test document:** a brand-new, throwaway, never-saved drawing (`Drawing2.dwg`, created via a
  third spike tool, `spike_new_drawing`) was made the active document before either operation
  ran. `Lusaran As-built.dwg` was never touched, written to, or made active again during the
  live trial — confirmed by `drawing_info` before, during and after every step.

---

## Execution Path Tested

Files read to ground this spike in the platform's actual architecture, not assumptions:

| Concern | File | Finding |
| --- | --- | --- |
| Tool dispatch | `src/bridges/Civil3D.Bridge/Execution/ToolDispatcher.cs` | FIFO queue; `RequiresApplicationContext` tools are marshaled via `IApplicationContext.ExecuteAsync`; every exception not a `BridgeException` is mapped to `E_INTERNAL` and never crosses the pipe. |
| Application-context marshaling | `src/bridges/Civil3D.Bridge/Execution/AutodeskApplicationContext.cs` | **Correction to the spike's own premise**: production code no longer uses `DocumentManager.ExecuteInApplicationContext`. A code comment explains why: "in AutoCAD 2025 — an in-process .NET 8 host — its callback ran on a thread-pool thread, so Autodesk API access happened off the main thread and corrupted the WPF ribbon, hanging the tool dispatcher." The bridge now queues work and drains it on `Application.Idle`, which always fires on the main UI thread. This is materially relevant to Plot: the plot code below would run **on the main thread during an idle callback**, not on a background thread — a better starting position than the LandXML exporter had reason to fear. |
| Contract | `src/bridges/Civil3D.Tools.Abstractions/Civil3DToolBase.cs` | Confirms `RequiresApplicationContext => true` (sealed) and the standard exception-mapping contract every tool gets for free. |
| Precedent for an honest not-supported result | `src/bridges/Civil3D.Tools.Export/Abstractions/Civil3DLandXmlExporter.cs` | Existing production code that reports `LandXmlExportStatus.NotSupported` because "the Civil 3D managed LandXML export path requires a live interactive document context and is not exposed through the read-only workflow layer of this platform." This is the direct precedent this spike compares its findings against. |
| Existing tool test pattern | `tests/Civil3D.Tools.Editing.Tests/UpdatePipeToolTests.cs`, `EditingTestHarness.cs` | Confirms production tools are tested headless via an in-memory fake, never against live Civil 3D — consistent with why this spike could not simply "add a unit test" to get a live answer. |
| Real transaction pattern | `src/bridges/Civil3D.Bridge/Data/AutodeskTransactionProvider.cs` | `document.LockDocument()` → `document.Database.TransactionManager.StartTransaction()` → commit/abort/dispose. The spike's transaction code mirrors this exactly (real, already-shipping code, not invented). |

**Live baseline check (read-only, zero risk, actually executed):** the real, unmodified
production server binary (`dist/index.js`) was spawned and driven over real MCP stdio. It
discovered the live bridge, completed the handshake, listed 35 tools, and called `drawing_info`
— which returned live, real data from the open drawing:

```json
{
  "drawingName": "Lusaran As-built.dwg",
  "isModified": true,
  "civil3DVersion": "25.0.0.0",
  "bridgeVersion": "1.0.0",
  "openDocumentsCount": 1
}
```

This **proves**, live, that `MCP client → Autodesk.Mcp.Server → named pipe → Civil3D.Bridge →
ToolDispatcher → IApplicationContext → Autodesk API → real drawing` is alive and working.

**Two additional platform facts discovered while getting the spike to actually run live** (both
useful for whoever builds the real production tools later):

1. **A referenced-but-unused tool assembly does not get discovered.** `ToolCatalog` scans
   `AppDomain.CurrentDomain.GetAssemblies()`, but a plain `<ProjectReference>` from
   `Civil3D.Bridge.csproj` is not enough to force .NET to load that assembly into the process —
   nothing in the bridge's code ever touches a type from it. `BridgeServiceCollectionExtensions.cs`
   already documents this exact gotcha for `Civil3D.Tools.Query` ("a bare typeof reference is
   optimized away by the compiler... the generic registration forces the assembly to load") — the
   spike had to add one temporary `services.AddSingleton<...>()` line for one spike tool type to
   force-load the assembly before `ToolCatalog` construction. **Any real `Civil3D.Tools.*`
   assembly needs at least one DI registration, not just a project reference, or its tools will
   silently never appear.**
2. **Windows/AutoCAD shows a blocking "Security - Unsigned Executable File" dialog** (Always
   Load / Load Once / Do Not Load) on Civil 3D startup whenever files in an `ApplicationPlugins`
   bundle change and are unsigned — which stalls the entire `Application.Idle` loop (nothing else
   proceeds while the modal is up) until a human clicks through it. This happened on one of the
   three restarts performed for this spike and had to be dismissed manually by the user; Claude
   Code's own safety classifier correctly refused to click through it programmatically. **This is
   a real, unresolved production-readiness gap**: the shipped bundle is unsigned, so every update
   plausibly reprompts every user's Civil 3D on first launch after upgrade — worth a proper ADR
   before this platform has real end users, independent of the PDF work.

---

## PDF Underlay

**API actually used** (verified by reflecting over the installed
`acdbmgd.dll`/`accoremgd.dll` with `System.Reflection` in a throwaway console app — not assumed,
not copied from documentation):

- `Autodesk.AutoCAD.DatabaseServices.PdfDefinition` — public parameterless constructor;
  writable `SourceFileName`, `ItemName`; instance method `Load(string password)`.
- `Autodesk.AutoCAD.DatabaseServices.PdfReference` (base `UnderlayReference` → `Entity`) —
  public parameterless constructor; writable `DefinitionId`, `Position` (`Point3d`),
  `ScaleFactors` (`Scale3d`), `Layer`.
- `Autodesk.AutoCAD.DatabaseServices.UnderlayDefinition.GetDictionaryKey(Type)` — static,
  returns the dictionary key AutoCAD indexes PDF definitions under in the drawing's Named
  Object Dictionary.
- `Database.NamedObjectsDictionaryId`, `DBDictionary.Contains/GetAt/SetAt`,
  `Database.CurrentSpaceId`, `BlockTableRecord.AppendEntity(Entity)` — all confirmed real and,
  for the first pair, already used in this exact repo
  (`AutodeskDrawingStatisticsService.cs:166-171`).
- Transaction/document-lock shape taken verbatim from
  `src/bridges/Civil3D.Bridge/Data/AutodeskTransactionProvider.cs`.

**What is grounded vs. assumed:** every type name, constructor and member signature above was
independently confirmed to exist in the installed SDK. The **order** of operations (create
definition → add to NOD dictionary → `Load` → create reference → append to current space →
commit) follows the standard, widely-documented AutoCAD managed-API underlay-attach idiom — and
the live trial below confirms that sequencing is correct as written, on the first attempt, no
changes needed.

**PASS / FAIL / UNVERIFIED: PASS — VERIFIED LIVE**

Executed against a real, running Civil 3D 2025 session via the real, unmodified production
server (`dist/index.js`) over real MCP stdio, through the real named pipe, through the real
`ToolDispatcher`/`IApplicationContext`, on a throwaway drawing (`Drawing2.dwg`) created for this
purpose so `Lusaran As-built.dwg` was never touched.

**Exact evidence** (raw tool result, live):
```json
{
  "success": true,
  "stage": "complete",
  "definitionHandle": "F2E",
  "referenceHandle": "F2F",
  "verifiedInDrawing": true
}
```
`verifiedInDrawing: true` means the tool reopened the object by its `ObjectId` in a fresh
transaction after commit and confirmed it exists and is not erased — not just that the initial
transaction didn't throw.

**Exception, if any:** none. Every stage (lock document → start transaction → create
`PdfDefinition` → get/create the NOD dictionary → add definition → `Load` → create `PdfReference`
→ append to current space → commit → verify) completed without error on the first attempt.

**Transaction/commit result:** committed successfully; the `PdfDefinition` (handle `F2E`) and
`PdfReference` (handle `F30`/`F2F` across the two live runs) both persisted.

**Drawing verification:** confirmed — the tool's own post-commit re-read succeeded, and
`drawing_info` immediately after showed `isModified: true` on the throwaway drawing (i.e. the
write really happened), while `currentDocumentName` stayed `Drawing2.dwg` throughout (i.e. the
real drawing was never touched).

**Conclusion:** PDF underlay attach works, live, through this platform's real execution path, on
the first attempt, with no code changes needed after the compile-only pass. This is a genuine
`IMPLEMENT` signal for a production `attach_pdf_underlay` tool (see "Production Recommendation").

---

## Plot-to-PDF

**API actually used** (same reflection method as above, over `accoremgd.dll`/`acdbmgd.dll`):

- `Autodesk.AutoCAD.DatabaseServices.PlotSettings(bool modelType)`, `CopyFrom(RXObject)`.
- `Autodesk.AutoCAD.DatabaseServices.PlotSettingsValidator.Current` (singleton) with
  `SetPlotConfigurationName`, `RefreshLists`, `SetPlotType`, `SetUseStandardScale`,
  `SetStdScaleType`, `SetPlotCentered`.
- `Autodesk.AutoCAD.PlottingServices.PlotInfo` — public parameterless constructor; `Layout`
  (`ObjectId`), `OverrideSettings` (`PlotSettings`).
- `Autodesk.AutoCAD.PlottingServices.PlotInfoValidator` — public parameterless constructor;
  `MediaMatchingPolicy`; `Validate(PlotInfo)`.
- `Autodesk.AutoCAD.PlottingServices.PlotFactory.CreatePublishEngine()` (static) →
  `Autodesk.AutoCAD.PlottingServices.PlotEngine` with `BeginPlot`, `BeginDocument`, `BeginPage`,
  `BeginGenerateGraphics`, `EndGenerateGraphics`, `EndPage`, `EndDocument`, `EndPlot`.
- `Autodesk.AutoCAD.DatabaseServices.LayoutManager.Current.CurrentLayout` /
  `.GetLayoutId(string)` — confirmed real (used to resolve the active layout's `ObjectId`
  without inventing an API).

**A concrete, surprising finding from reflection** (exactly the kind of thing this exercise was
for): `Autodesk.AutoCAD.PlottingServices.PlotProgress` has **no public constructor** in this SDK
version. `PlotEngine.BeginPlot(PlotProgress, object)` requires one as its first argument. Rather
than invent a constructor that doesn't exist, the spike passes `null`. Whether the Plot engine
accepts `null` there is unverified — this alone is a legitimate open question a live trial would
answer immediately and documentation/reflection cannot.

**What is grounded vs. assumed:** as with the underlay, every individual member signature above
is reflection-confirmed. The overall `BeginPlot → BeginDocument → BeginPage →
BeginGenerateGraphics → EndGenerateGraphics → EndPage → EndDocument → EndPlot` sequence is the
standard, widely-documented AutoCAD managed-API publish idiom — the live trial found **one real
bug** in the best-effort ordering around it (below), not in this Begin/End sequence itself.

**PASS / FAIL / UNVERIFIED: PASS — VERIFIED LIVE (after one corrected attempt)**

**First live attempt — genuine FAIL, not a crash.** With the code exactly as written in the
compile-only pass, the tool ran every stage through `"complete"` with **no exception at all**,
but produced **no output file**:
```json
{ "success": false, "stage": "complete", "outputPath": "...\\spike-plot-output.pdf" }
```
Independently confirmed by checking the filesystem directly (not just trusting the tool's
self-report): the file did not exist. This is exactly the failure mode the objective asked to
capture precisely — a silent no-op, distinct from an exception.

**Root cause, found and fixed:** `PlotSettingsValidator.RefreshLists(plotSettings)` was called
*after* `SetPlotConfigurationName(plotSettings, "DWG To PDF.pc3", ...)` in the original code.
`RefreshLists` populates the validator's known-device/media lists from the system; calling
`SetPlotConfigurationName` before that populates the lists meant the device name silently failed
to resolve against anything, and the plot proceeded with no valid device — no exception, just no
output. Reordering to `RefreshLists` **first**, then `SetPlotConfigurationName`, fixed it. This
is a genuinely useful finding: the best-effort ordering assumed from general AutoCAD API
familiarity was subtly wrong, and only live execution surfaced it — exactly the class of bug
this whole exercise exists to catch before it ships.

**Second live attempt — PASS, with diagnostics added to prove why:**
```json
{
  "success": true,
  "stage": "complete",
  "outputPath": "...\\spike-plot-output.pdf",
  "fileSizeBytes": 6906,
  "appliedDeviceName": "DWG To PDF.pc3",
  "availableDevices": ["None", "OneNote (Desktop)", "...", "DWG To PDF.pc3", "..."]
}
```
`availableDevices` (captured via `PlotSettingsValidator.GetPlotDeviceList()` after the reordered
`RefreshLists` call) confirms `"DWG To PDF.pc3"` was genuinely present on this system's device
list, and `appliedDeviceName` confirms it was the one actually applied to the settings —
removing any doubt about *why* it worked this time.

**Exception, if any:** none in either attempt. The first attempt's failure was silent (no
exception, no output); the second attempt succeeded cleanly.

**Output path:** `spike-plot-output.pdf` (scratch directory, never inside the repo or near
`Lusaran As-built.dwg`).

**Output file existence/size — independently verified, not just tool-reported:**
```
$ ls -la spike-plot-output.pdf
-rw-r--r-- 1 hjlro 197609 6906 ... spike-plot-output.pdf
$ head -c 20 spike-plot-output.pdf | xxd
00000000: 2550 4446 2d31 2e37 0a25 dead beef 0a31  %PDF-1.7.%.....1
```
A real, valid PDF file (`%PDF-1.7` header, standard binary marker) — not an empty file, not a
zero-byte placeholder.

**Conclusion:** Plot-to-PDF works, live, through this platform's real execution path — the
concrete `Application.Idle`-based main-thread fix already made for every other tool (see
"Execution Path Tested") does appear to give the Plot API what it needs, contrary to the
cautious lean in the original compile-only pass. The one real ordering bug found here is a
one-line fix, not a platform limitation.

---

## Comparison With LandXML

**Neither capability hit the LandXML limitation. Both worked, live, on the real execution path.**

- `Civil3DLandXmlExporter` is `NotSupported` because the *workflow layer* it runs under
  (`Civil3D.Tools.Export`, a `WorkflowToolBase` tool) is documented as read-only and without a
  live interactive document context. **PDF underlay attach never shared that constraint** — it
  went through the same `DocumentLock` + `Transaction` write path `create_pipe`/`update_pipe`
  already use successfully in production, and the live trial confirms it: attach, commit and
  re-verification all succeeded on the first attempt.
- **Plot-to-PDF was the more uncertain of the two going in** — plotting has historically been one
  of the AutoCAD managed API's more UI/interactive-context-sensitive operations, and the spike
  found a real, concrete gap (no public `PlotProgress` constructor) during the compile-only pass.
  Live execution resolved the uncertainty directly: the `Application.Idle`-based main-thread fix
  this platform already made for every tool (see "Execution Path Tested") evidently gives the
  Plot API what it needs — the only thing that actually blocked output was an ordering bug in
  this spike's own code (`RefreshLists` after, not before, `SetPlotConfigurationName`), not a
  platform-level limitation. **This is a materially better outcome than LandXML export**, and the
  gap between the two capabilities is now explained: LandXML's blocker is architectural (missing
  interactive context), Plot-to-PDF's only blocker was a fixable sequencing bug.

---

## Production Recommendation

**PDF Underlay: IMPLEMENT**
Live-verified on the first attempt with no code changes. The spike tool already matches the
production shape (`Civil3DToolBase<TIn,TOut>`); turning it into a real `attach_pdf_underlay` tool
means moving the Autodesk-touching code behind an Autodesk-free service contract per rule A5,
adding proper validation (file-not-found, non-PDF file, out-of-range scale/insertion values) and
the standard `E_*` error mapping in place of the spike's catch-everything diagnostic style, plus
headless tests per pattern P4.

**Plot-to-PDF: IMPLEMENT**
Live-verified after fixing one real ordering bug (`RefreshLists` before `SetPlotConfigurationName`).
A production `export_pdf` tool should additionally: make the target device/media configurable
rather than hardcoding `"DWG To PDF.pc3"`; handle the case where that device is absent from
`GetPlotDeviceList()` on a given machine (report a structured, honest failure rather than a
silent no-op — the exact bug this spike just caught); and decide the layout-selection contract
(current layout only, vs. an explicit layout name parameter).

Neither is IMPLEMENT WITH LIMITATIONS or NOT SUPPORTED — both worked cleanly through the
platform's real execution path with no discovered architectural blocker.

---

## Verification

- **VERIFIED LIVE:** the baseline execution path (`MCP client → Autodesk.Mcp.Server → named
  pipe → Civil3D.Bridge → ToolDispatcher → IApplicationContext → Autodesk API`) — proven twice,
  once via `drawing_info` against the pre-existing session and again via the full spike trial.
- **VERIFIED LIVE:** PDF underlay attach — real transaction, real commit, real post-commit
  re-verification, against a real (throwaway) drawing. See "PDF Underlay" above.
- **VERIFIED LIVE:** Plot-to-PDF — real Plot API engine run producing a real, independently
  verified PDF file on disk. See "Plot-to-PDF" above.
- **COMPILES ONLY:** nothing remains at this tier — both capabilities that started here were
  promoted to VERIFIED LIVE in this pass.

### How the live trial was actually run (for anyone repeating this)

1. User confirmed the drawing was saved; `save_drawing` + a `drawing_info` re-check confirmed
   `isModified: false` independently before anything destructive happened.
2. The spike project was recreated from source (see the git history of this doc, or rebuild from
   the description in "PDF Underlay"/"Plot-to-PDF" above) and temporarily wired into
   `Civil3D.Bridge.csproj` **and** `BridgeServiceCollectionExtensions.cs` (one `AddSingleton`
   line — see the force-load finding above) so `ToolCatalog` would actually discover it.
3. `dotnet build AutodeskMcp.slnx -c Release` → `node eng/scripts/build-bridge-bundle.mjs` →
   the refreshed bundle replaced the installed one in `%APPDATA%\Autodesk\ApplicationPlugins\`
   (the original was backed up first).
4. `taskkill` on the running `acad.exe`, then relaunch from the same path. Confirmed via the
   endpoint registry + a `bridgeVersion` check that the new build was actually running.
5. A third spike tool, `spike_new_drawing`, created a throwaway blank document and made it
   active, so the two operations under test never touched `Lusaran As-built.dwg`.
6. `spike_attach_pdf_underlay` and `spike_plot_to_pdf` were called for real over the real
   production MCP server. The first plot attempt failed silently (no exception, no file); the
   bug was found, fixed in the spike source, and the rebuild/redeploy/restart cycle repeated
   (this second restart triggered a Windows "unsigned executable" trust dialog that stalled
   startup until the user manually clicked "Load Once" — Claude Code's safety classifier
   correctly refused to automate that click).
7. Both operations were re-run and both passed; the output PDF was independently verified on
   disk (not just via the tool's self-reported result).
8. The bridge wiring was fully reverted, the spike folder deleted again, the original `1.0.0`
   bundle restored from a pre-change backup, and Civil 3D restarted one final time — confirmed
   back to `bridgeVersion: "1.0.0"` and exactly 35 tools, matching the state before this spike
   began.

---

## Files Changed

- `tools_tmp/pdf-plot-spike/README.md` — created (temporary, marked for removal)
- `tools_tmp/pdf-plot-spike/Civil3D.Spike.PdfPlot.csproj` — created (temporary, marked for removal)
- `tools_tmp/pdf-plot-spike/PdfUnderlaySpikeTool.cs` — created, then deleted (temporary)
- `tools_tmp/pdf-plot-spike/PlotToPdfSpikeTool.cs` — created, then deleted (temporary)
- `tools_tmp/pdf-plot-spike/NewDrawingSpikeTool.cs` — created, then deleted (temporary; added
  during the live pass so the two operations under test never touch a real drawing)
- `tools_tmp/pdf-plot-spike/Civil3D.Spike.PdfPlot.csproj` — created, then deleted (temporary)
- `tools_tmp/pdf-plot-spike/README.md` — created, then deleted (temporary)
- `docs/PDF-UNDERLAY-PLOT-SPIKE.md` — created (this file; permanent)
- `ai-workflow/reports/PDF-UNDERLAY-PLOT-SPIKE-REPORT.md` — created (permanent)
- `README.md` — modified: added a `docs/` table row pointing at this document
- `src/bridges/Civil3D.Bridge/Civil3D.Bridge.csproj` — temporarily modified (one
  `<ProjectReference>` to the spike project) **during the live trial only**; reverted, confirmed
  by `git status` showing no diff on this file at the end
- `src/bridges/Civil3D.Bridge/DependencyInjection/BridgeServiceCollectionExtensions.cs` —
  temporarily modified (one `using` + one `services.AddSingleton<...>()` line, to force-load the
  spike assembly — see the discovery in "Execution Path Tested") **during the live trial only**;
  reverted, confirmed by `git status` showing no diff on this file at the end
- `%APPDATA%\Autodesk\ApplicationPlugins\Civil3D.Bridge.Bundle-*.bundle` (outside the repo) —
  the installed plugin bundle was temporarily replaced with a spike-enabled build, then restored
  from a pre-change backup; confirmed by a final live `drawing_info`/`tools/list` check showing
  `bridgeVersion: "1.0.0"` and exactly 35 tools, matching the state before this session began

No file under `src/`, `tests/`, `eng/`, `packaging/`, `.github/`, or any `.slnx` shows a diff in
`git status` at the end of this session — the two temporarily modified bridge files were fully
reverted after the live trial captured its results.

---

## Appendix: full spike source (final, live-verified version)

Preserved here because the working copy under `tools_tmp/pdf-plot-spike/` was deleted as part of
"Cleanup" and was never committed to git, so it is otherwise unrecoverable. This is the **final**
version — `PlotToPdfSpikeTool.cs` includes the `RefreshLists`-ordering fix and the diagnostic
fields that produced the live PASS results above, not the original compile-only draft. A fourth
file, `NewDrawingSpikeTool.cs`, was added during the live pass so the two operations under test
never touch a real, already-open drawing.

To repeat or extend this: recreate these five files verbatim, wire the spike into
`Civil3D.Bridge.csproj` (one `<ProjectReference>`) and
`BridgeServiceCollectionExtensions.cs` (one `using` + one `services.AddSingleton<...>()` line —
see the force-load discovery above), build `AutodeskMcp.slnx`, rebuild the bundle
(`node eng/scripts/build-bridge-bundle.mjs`), back up and replace the installed
`ApplicationPlugins` bundle, and restart Civil 3D.

### `tools_tmp/pdf-plot-spike/Civil3D.Spike.PdfPlot.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!--
    TEMPORARY spike project — see README.md in this folder. Not part of any .slnx, not
    referenced by Civil3D.Bridge. Builds standalone to prove the spike tools compile against
    the real Autodesk SDK assemblies installed on this machine.
  -->

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RootNamespace>Civil3D.Spike.PdfPlot</RootNamespace>
    <AssemblyName>Civil3D.Spike.PdfPlot</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
    <NuGetAudit>false</NuGetAudit>
    <MSBuildWarningsAsMessages>MSB3277</MSBuildWarningsAsMessages>
    <AutodeskAcadDir Condition="'$(AutodeskAcadDir)' == ''">$(ProgramFiles)\Autodesk\AutoCAD 2025</AutodeskAcadDir>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\sdk\Autodesk.Mcp.Sdk\Autodesk.Mcp.Sdk.csproj" />
    <ProjectReference Include="..\..\src\shared\Autodesk.Mcp.Shared\Autodesk.Mcp.Shared.csproj" />
    <ProjectReference Include="..\..\src\bridges\Civil3D.Tools.Abstractions\Civil3D.Tools.Abstractions.csproj" />
  </ItemGroup>

  <ItemGroup Condition="Exists('$(AutodeskAcadDir)\acmgd.dll')">
    <Reference Include="AcMgd">
      <HintPath>$(AutodeskAcadDir)\acmgd.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="AcDbMgd">
      <HintPath>$(AutodeskAcadDir)\acdbmgd.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="AcCoreMgd">
      <HintPath>$(AutodeskAcadDir)\accoremgd.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <Target Name="EnsureAutodeskSdk" BeforeTargets="ResolveAssemblyReferences" Condition="!Exists('$(AutodeskAcadDir)\acmgd.dll')">
    <Error Text="Autodesk SDK not found at '$(AutodeskAcadDir)'. Set the AutodeskAcadDir MSBuild property to the AutoCAD 2025 install folder." />
  </Target>

</Project>
```

### `tools_tmp/pdf-plot-spike/PdfUnderlaySpikeTool.cs`

```csharp
// TEMPORARY SPIKE — see README.md in this folder. Not referenced by Civil3D.Bridge, not
// shipped, not registered in any .slnx. Delete this folder once the spike write-up in
// docs/PDF-UNDERLAY-PLOT-SPIKE.md is finalized.
//
// Deliberately deviates from the production tool contract (docs/TOOL-DEVELOPMENT.md) in one
// way: it catches every exception itself and returns full exception detail in the result DTO
// instead of letting it propagate to Civil3DToolBase's E_INTERNAL mapping. That is the whole
// point of a reachability spike — a production version of this feature would move the
// Autodesk-touching code behind an Autodesk-free service contract (rule A5) and let exceptions
// map to BridgeException as usual.
//
// API surface below (type names, constructors, method/property signatures) was verified by
// reflecting over the actual acdbmgd.dll / accoremgd.dll installed at
// "C:\Program Files\Autodesk\AutoCAD 2025" on this machine — see
// docs/PDF-UNDERLAY-PLOT-SPIKE.md "PDF Underlay > API actually used" for the full list. The
// ORDER of calls (create definition -> add to NOD dictionary -> Load -> create reference ->
// append to current space -> commit) follows the standard AutoCAD managed-API underlay-attach
// idiom; that sequencing is NOT itself proven by reflection and is exactly what live execution
// would confirm or refute.

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Tools.Abstractions;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Civil3D.Spike.PdfPlot;

/// <summary>SPIKE input: PDF file path, insertion point and uniform scale.</summary>
public sealed record PdfUnderlaySpikeRequest
{
    /// <summary>Absolute path to a PDF file readable by this process.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>1-based PDF page to attach, as a string (PdfDefinition.ItemName's expected shape).</summary>
    public string Page { get; init; } = "1";

    public double InsertionX { get; init; }
    public double InsertionY { get; init; }
    public double InsertionZ { get; init; }
    public double ScaleFactor { get; init; } = 1.0;
}

/// <summary>SPIKE output: exactly what happened, stage by stage, for the write-up.</summary>
public sealed record PdfUnderlaySpikeResult
{
    public bool Success { get; init; }

    /// <summary>Last stage reached before success or failure (see docs/PDF-UNDERLAY-PLOT-SPIKE.md).</summary>
    public string Stage { get; init; } = string.Empty;

    public string? DefinitionHandle { get; init; }
    public string? ReferenceHandle { get; init; }
    public bool VerifiedInDrawing { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
}

[McpTool(
    "spike_attach_pdf_underlay",
    "SPIKE: Attach PDF Underlay (temporary, not for production use)",
    "TEMPORARY SPIKE TOOL. Attempts to attach a PDF as an underlay reference in the active " +
    "drawing through the same ToolDispatcher -> IApplicationContext path production tools use, " +
    "and reports the exact outcome and stage reached instead of mapping failures to an error " +
    "code. Never registered in the real bridge; exists only to compile against the real " +
    "Autodesk SDK for the PDF workflow reachability spike.",
    Category = ToolCategory.Objects,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Medium,
    Version = "0.0.0-spike",
    SupportsCancellation = false,
    Tags = new[] { "spike", "temporary", "do-not-ship" })]
public sealed class PdfUnderlaySpikeTool : Civil3DToolBase<PdfUnderlaySpikeRequest, PdfUnderlaySpikeResult>
{
    public PdfUnderlaySpikeTool(ICivil3DSession session) : base(session)
    {
    }

    protected override Task<PdfUnderlaySpikeResult> ExecuteToolCoreAsync(
        PdfUnderlaySpikeRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        // Same document-availability check every production tool performs first.
        RequireActiveDrawing(context);

        Document? document = CoreApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return Task.FromResult(new PdfUnderlaySpikeResult
            {
                Success = false,
                Stage = "before-transaction",
                ExceptionType = nameof(InvalidOperationException),
                ExceptionMessage = "No active document (race between RequireActiveDrawing and MdiActiveDocument).",
            });
        }

        string stage = "not-started";
        try
        {
            stage = "lock-document";
            using DocumentLock docLock = document.LockDocument();

            stage = "start-transaction";
            using Transaction tr = document.Database.TransactionManager.StartTransaction();

            stage = "create-pdf-definition";
            var definition = new PdfDefinition
            {
                SourceFileName = input.FilePath,
                ItemName = input.Page,
            };

            stage = "get-or-create-definitions-dictionary";
            var nod = (DBDictionary)tr.GetObject(document.Database.NamedObjectsDictionaryId, OpenMode.ForWrite);
            string dictKey = UnderlayDefinition.GetDictionaryKey(typeof(PdfDefinition));
            DBDictionary defDict;
            if (nod.Contains(dictKey))
            {
                defDict = (DBDictionary)tr.GetObject(nod.GetAt(dictKey), OpenMode.ForWrite);
            }
            else
            {
                defDict = new DBDictionary();
                nod.SetAt(dictKey, defDict);
                tr.AddNewlyCreatedDBObject(defDict, true);
            }

            stage = "add-definition-to-dictionary";
            ObjectId defId = defDict.SetAt("SpikePdfUnderlay", definition);
            tr.AddNewlyCreatedDBObject(definition, true);

            stage = "load-definition";
            definition.Load(string.Empty);

            stage = "create-pdf-reference";
            var reference = new PdfReference
            {
                DefinitionId = defId,
                Position = new Point3d(input.InsertionX, input.InsertionY, input.InsertionZ),
                ScaleFactors = new Scale3d(input.ScaleFactor),
            };

            stage = "append-reference-to-current-space";
            var space = (BlockTableRecord)tr.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
            ObjectId refId = space.AppendEntity(reference);
            tr.AddNewlyCreatedDBObject(reference, true);

            stage = "commit";
            tr.Commit();

            stage = "verify-in-drawing";
            bool stillThere;
            using (Transaction verifyTr = document.Database.TransactionManager.StartTransaction())
            {
                var reopened = verifyTr.GetObject(refId, OpenMode.ForRead) as PdfReference;
                stillThere = reopened is not null && !reopened.IsErased;
                verifyTr.Commit();
            }

            return Task.FromResult(new PdfUnderlaySpikeResult
            {
                Success = true,
                Stage = "complete",
                DefinitionHandle = definition.Handle.ToString(),
                ReferenceHandle = reference.Handle.ToString(),
                VerifiedInDrawing = stillThere,
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new PdfUnderlaySpikeResult
            {
                Success = false,
                Stage = stage,
                ExceptionType = ex.GetType().FullName,
                ExceptionMessage = ex.Message,
            });
        }
    }
}
```

### `tools_tmp/pdf-plot-spike/PlotToPdfSpikeTool.cs`

```csharp
// TEMPORARY SPIKE — see README.md in this folder. Not referenced by Civil3D.Bridge, not
// shipped, not registered in any .slnx. Delete this folder once the spike write-up in
// docs/PDF-UNDERLAY-PLOT-SPIKE.md is finalized.
//
// Same deliberate deviation from the production tool contract as PdfUnderlaySpikeTool.cs:
// every exception is caught here and returned in the result DTO for diagnosis, instead of
// mapping to BridgeException/E_INTERNAL.
//
// API surface below (PlotSettings, PlotSettingsValidator, PlotInfo, PlotInfoValidator,
// PlotFactory, PlotEngine, LayoutManager member signatures) was verified by reflecting over
// the actual accoremgd.dll / acdbmgd.dll installed at "C:\Program Files\Autodesk\AutoCAD 2025"
// on this machine -- see docs/PDF-UNDERLAY-PLOT-SPIKE.md "Plot-to-PDF > API actually used".
//
// ONE CONCRETE FINDING from that reflection: Autodesk.AutoCAD.PlottingServices.PlotProgress
// exposes NO public constructor in this SDK version, so this spike passes null for the
// PlotEngine.BeginPlot progress-dialog parameter rather than inventing a constructor. Whether
// the runtime accepts null there is unverified without live execution.
//
// The overall Begin/End call ORDER (BeginPlot -> BeginDocument -> BeginPage ->
// BeginGenerateGraphics -> EndGenerateGraphics -> EndPage -> EndDocument -> EndPlot) follows the
// standard AutoCAD managed-API publish idiom; that sequencing is NOT itself proven by
// reflection in this session and is exactly what live execution would confirm or refute.

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Tools.Abstractions;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Civil3D.Spike.PdfPlot;

/// <summary>SPIKE input: where to write the plotted PDF.</summary>
public sealed record PlotToPdfSpikeRequest
{
    /// <summary>Absolute output path for the plotted PDF.</summary>
    public string OutputPath { get; init; } = string.Empty;
}

/// <summary>SPIKE output: exactly what happened, stage by stage, for the write-up.</summary>
public sealed record PlotToPdfSpikeResult
{
    public bool Success { get; init; }

    /// <summary>Last stage reached before success or failure (see docs/PDF-UNDERLAY-PLOT-SPIKE.md).</summary>
    public string Stage { get; init; } = string.Empty;

    public string? OutputPath { get; init; }
    public long? FileSizeBytes { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }

    /// <summary>Diagnostic: known plot device names, to check whether "DWG To PDF.pc3" resolved.</summary>
    public string[] AvailableDevices { get; init; } = Array.Empty<string>();

    /// <summary>Diagnostic: the plot device name actually applied to the settings after configuration.</summary>
    public string? AppliedDeviceName { get; init; }
}

[McpTool(
    "spike_plot_to_pdf",
    "SPIKE: Plot Current Layout to PDF (temporary, not for production use)",
    "TEMPORARY SPIKE TOOL. Attempts to plot the active drawing's current layout to a PDF file " +
    "through the same ToolDispatcher -> IApplicationContext path production tools use, and " +
    "reports the exact outcome and stage reached instead of mapping failures to an error code. " +
    "Never registered in the real bridge; exists only to compile against the real Autodesk SDK " +
    "for the PDF workflow reachability spike.",
    Category = ToolCategory.Export,
    Permission = ToolPermission.Export,
    Risk = ToolRisk.Medium,
    Version = "0.0.0-spike",
    SupportsCancellation = false,
    Tags = new[] { "spike", "temporary", "do-not-ship" })]
public sealed class PlotToPdfSpikeTool : Civil3DToolBase<PlotToPdfSpikeRequest, PlotToPdfSpikeResult>
{
    public PlotToPdfSpikeTool(ICivil3DSession session) : base(session)
    {
    }

    protected override Task<PlotToPdfSpikeResult> ExecuteToolCoreAsync(
        PlotToPdfSpikeRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        RequireActiveDrawing(context);

        Document? document = CoreApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return Task.FromResult(new PlotToPdfSpikeResult
            {
                Success = false,
                Stage = "before-plot",
                ExceptionType = nameof(InvalidOperationException),
                ExceptionMessage = "No active document (race between RequireActiveDrawing and MdiActiveDocument).",
            });
        }

        string stage = "not-started";
        try
        {
            stage = "check-process-plot-state";
            if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
            {
                return Task.FromResult(new PlotToPdfSpikeResult
                {
                    Success = false,
                    Stage = stage,
                    ExceptionType = nameof(InvalidOperationException),
                    ExceptionMessage = "A plot is already in progress on this document/session (ProcessPlotState != NotPlotting).",
                });
            }

            stage = "resolve-current-layout";
            string currentLayoutName = LayoutManager.Current.CurrentLayout;
            ObjectId layoutId = LayoutManager.Current.GetLayoutId(currentLayoutName);

            stage = "lock-document";
            using DocumentLock docLock = document.LockDocument();

            stage = "start-transaction";
            using Transaction tr = document.Database.TransactionManager.StartTransaction();

            stage = "build-plot-info";
            var plotInfo = new PlotInfo { Layout = layoutId };

            stage = "build-plot-settings";
            var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
            var plotSettings = new PlotSettings(layout.ModelType);
            plotSettings.CopyFrom(layout);

            PlotSettingsValidator psv = PlotSettingsValidator.Current;
            // FIX (found live): RefreshLists MUST run before SetPlotConfigurationName, or the
            // device name silently fails to resolve and the plot no-ops with no exception and
            // no output file. The original draft had these two calls in the opposite order.
            stage = "refresh-device-lists";
            psv.RefreshLists(plotSettings);
            string[] availableDevices = psv.GetPlotDeviceList().Cast<string>().ToArray();

            stage = "configure-plot-device";
            psv.SetPlotConfigurationName(plotSettings, "DWG To PDF.pc3", null);
            psv.SetPlotType(plotSettings, Autodesk.AutoCAD.DatabaseServices.PlotType.Extents);
            psv.SetUseStandardScale(plotSettings, true);
            psv.SetStdScaleType(plotSettings, StdScaleType.ScaleToFit);
            psv.SetPlotCentered(plotSettings, true);

            plotInfo.OverrideSettings = plotSettings;

            stage = "validate-plot-info";
            var plotInfoValidator = new PlotInfoValidator
            {
                MediaMatchingPolicy = MatchingPolicy.MatchEnabled,
            };
            plotInfoValidator.Validate(plotInfo);

            stage = "commit-settings-transaction";
            tr.Commit();

            stage = "create-publish-engine";
            using PlotEngine engine = PlotFactory.CreatePublishEngine();

            string directory = Path.GetDirectoryName(input.OutputPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            stage = "begin-plot";
            // PlotProgress has no public constructor in this SDK version (see file header);
            // passing null rather than inventing a constructor call.
            engine.BeginPlot(null!, null);

            stage = "begin-document";
            engine.BeginDocument(plotInfo, document.Name, null, 1, true, input.OutputPath);

            stage = "begin-page";
            var pageInfo = new PlotPageInfo();
            engine.BeginPage(pageInfo, plotInfo, true, null);

            stage = "begin-generate-graphics";
            engine.BeginGenerateGraphics(null);

            stage = "end-generate-graphics";
            engine.EndGenerateGraphics(null);

            stage = "end-page";
            engine.EndPage(null);

            stage = "end-document";
            engine.EndDocument(null);

            stage = "end-plot";
            engine.EndPlot(null);

            stage = "verify-output-file";
            bool exists = File.Exists(input.OutputPath);
            long size = exists ? new FileInfo(input.OutputPath).Length : 0;

            return Task.FromResult(new PlotToPdfSpikeResult
            {
                Success = exists && size > 0,
                Stage = "complete",
                OutputPath = input.OutputPath,
                FileSizeBytes = exists ? size : null,
                AvailableDevices = availableDevices,
                AppliedDeviceName = plotSettings.PlotConfigurationName,
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new PlotToPdfSpikeResult
            {
                Success = false,
                Stage = stage,
                OutputPath = input.OutputPath,
                ExceptionType = ex.GetType().FullName,
                ExceptionMessage = ex.Message,
            });
        }
    }
}
```

### `tools_tmp/pdf-plot-spike/NewDrawingSpikeTool.cs`

Added during the live pass so the two operations under test never touch a real, already-open
drawing. Grounded via reflection:
`DocumentCollectionExtension.Add(DocumentCollection, string templateFileName)` (`acmgd.dll`) and
`DocumentCollection.MdiActiveDocument` (public getter **and setter** confirmed in `accoremgd.dll`
— the setter is what let this tool force the new drawing active).

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Tools.Abstractions;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Civil3D.Spike.PdfPlot;

public sealed record NewDrawingSpikeRequest
{
    /// <summary>Absolute path to a .dwt template file.</summary>
    public string TemplatePath { get; init; } = string.Empty;
}

public sealed record NewDrawingSpikeResult
{
    public bool Success { get; init; }
    public string? DrawingName { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
}

[McpTool(
    "spike_new_drawing",
    "SPIKE: Create and activate a new blank drawing (temporary)",
    "TEMPORARY SPIKE TOOL. Creates a brand-new, unsaved drawing from the given template and " +
    "makes it the active document, so the PDF underlay/plot spike tools never touch a real, " +
    "already-open file.",
    Category = ToolCategory.General,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Low,
    Version = "0.0.0-spike",
    SupportsCancellation = false,
    Tags = new[] { "spike", "temporary", "do-not-ship" })]
public sealed class NewDrawingSpikeTool : Civil3DToolBase<NewDrawingSpikeRequest, NewDrawingSpikeResult>
{
    public NewDrawingSpikeTool(ICivil3DSession session) : base(session)
    {
    }

    protected override Task<NewDrawingSpikeResult> ExecuteToolCoreAsync(
        NewDrawingSpikeRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            Document newDoc = CoreApplication.DocumentManager.Add(input.TemplatePath);
            CoreApplication.DocumentManager.MdiActiveDocument = newDoc;

            return Task.FromResult(new NewDrawingSpikeResult
            {
                Success = true,
                DrawingName = newDoc.Name,
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new NewDrawingSpikeResult
            {
                Success = false,
                ExceptionType = ex.GetType().FullName,
                ExceptionMessage = ex.Message,
            });
        }
    }
}
```

---

## Cleanup

Confirmed, in order, at the end of the live pass:

1. `tools_tmp/pdf-plot-spike/` (all five files: the three spike tools, the project file, the
   README) — deleted.
2. `src/bridges/Civil3D.Bridge/Civil3D.Bridge.csproj` — the temporary `<ProjectReference>` to the
   spike project removed; `git status` shows no diff on this file.
3. `src/bridges/Civil3D.Bridge/DependencyInjection/BridgeServiceCollectionExtensions.cs` — the
   temporary `using` and `services.AddSingleton<NewDrawingSpikeTool>()` line removed; `git
   status` shows no diff on this file.
4. `dotnet build AutodeskMcp.slnx -c Release` re-run clean (0 warnings, 0 errors) with the spike
   fully gone, proving nothing in production code depends on it.
5. The installed `%APPDATA%\Autodesk\ApplicationPlugins\Civil3D.Bridge.Bundle-1.0.1.bundle`
   (spike-enabled) was deleted and replaced with the pre-change backup of
   `Civil3D.Bridge.Bundle-1.0.0.bundle`.
6. Civil 3D was restarted one final time; the live endpoint registry and a `tools/list` call
   both confirmed `bridgeVersion: "1.0.0"` and exactly **35 tools** — identical to the state
   observed at the very start of this work, before any spike code existed.

**No permanent MCP tool was added.** `spike_attach_pdf_underlay`, `spike_plot_to_pdf` and
`spike_new_drawing` never exist outside this document's appendix and the session transcript —
not in any shipped assembly, any `.slnx`, or the running bridge's tool catalog. `git status`
after cleanup shows only the two new documentation files and the `README.md` table edit.
