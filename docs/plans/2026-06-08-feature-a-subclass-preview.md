# Feature A — Subclass Selection Perk Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On the level-up subclass picker, let the player right-click any subclass (available OR greyed-injected unresearched ones) to open a view-only "CLASS PERKS" wiki banner of that subclass's guaranteed class-track abilities.

**Architecture:** A new pure, Unity-free core `ClassPerkResolver` (mirroring `PerkPoolResolver`) turns a subclass's ordered class-track ability names into a de-duplicated `List<TacticalAbilityDef>`, unit-tested with fakes. A thin Harmony POSTFIX on the vanilla picker's populate seam (`SelectSpecializationDataBind.ModalShowHandler`) wires a right-click handler onto each native subclass button and clones-then-greys native buttons for the subclasses the screen omitted, routing every right-click into the existing `PerkWikiPanel`. Class perks are deterministic (always `Fixed`), so there is no randomness to resolve and the existing rolled-cell highlight rides along for free.

**Tech Stack:** C# (mod: `net472` against Assembly-CSharp + 0Harmony + UnityEngine; tests: `net8.0` + xUnit 2.9.2, link-compiling pure cores only), HarmonyLib, I2.Loc CSV localization.

---

## File Structure

| File | Create/Modify | Responsibility |
| --- | --- | --- |
| `src/ClassPerkResolver.cs` | Create | Pure core: ordered class-track ability names → de-duplicated `List<T>` of resolved defs; injected order-source + resolver, no Unity/TFTV/Harmony dependency. |
| `tests/PerkOracle.Tests/ClassPerkResolverTests.cs` | Create | xUnit tests for `ClassPerkResolver.Resolve<T>` using string fakes, mirroring `PerkPoolResolverTests`. |
| `tests/PerkOracle.Tests/PerkOracle.Tests.csproj` | Modify | Link-compile `..\..\src\ClassPerkResolver.cs` so the pure core unit-tests under net8. |
| `src/ClassPerkProvider.cs` | Create | Game-side adapter (Unity-touching): given a `SpecializationDef`, build the ordered class-track name list from `AbilityTrack.AbilitiesByLevel[].Ability`, call `ClassPerkResolver.Resolve` with a name→def resolver, return `List<TacticalAbilityDef>`. Also enumerates the full subclass set from `DefRepository` and computes the omitted (greyed) set. |
| `src/PerkWikiPanel.cs` | Modify | Add optional `titleTerm`/`titleFallback` params to `Open` and thread them through `BuildPanel` → `BuildTitle` so the banner can show "CLASS PERKS" instead of "POSSIBLE SKILLS". |
| `src/SelectSpecializationDataBindPatch.cs` | Create | Harmony POSTFIX on `SelectSpecializationDataBind.ModalShowHandler`: attach a right-click handler to each active native subclass button, and clone+grey a native button for each omitted subclass, also right-clickable. Opens the wiki via `PerkWikiPanel.Open(canvas, defs, null, ClassTitleTerm, ClassTitleFallback)`. |
| `src/SubclassWikiClickHandler.cs` | Create | Small `MonoBehaviour` (`IPointerClickHandler`) attached to each subclass button: on right-click, resolve that button's `SpecializationDef`'s class perks via `ClassPerkProvider` and open the view-only wiki banner. |
| `Assets/Localization/PerkOracle_Localization.csv` | Modify | Add the `PERKORACLE_WIKI_TITLE_CLASS` row ("CLASS PERKS") across all 8 languages. |

**Note — rides along for free (no new code):** the rolled-cell highlight + candidate drill-down come from the existing `SetSkillStatePatch` / `OnCancelInputHandlerPatch` wherever progression cells render; the subclass picker shows class buttons (not progression cells), so on this screen the right-click banner is the new affordance and the highlight feature is untouched. No registration changes are needed in `PerkOracleMain` — `harmony.PatchAll(Assembly.GetExecutingAssembly())` (PerkOracleMain.cs:70) auto-discovers the new `[HarmonyPatch]` classes, and the CSV loads via the existing `LoadLocalization` (PerkOracleMain.cs:102).

---

## Task 1 — `ClassPerkResolver` pure core (de-dup + resolve)

**Files:**
- Create: `src/ClassPerkResolver.cs`
- Test: `tests/PerkOracle.Tests/ClassPerkResolverTests.cs`
- Modify: `tests/PerkOracle.Tests/PerkOracle.Tests.csproj`

- [ ] **Step 1: Write the failing test**

Create `tests/PerkOracle.Tests/ClassPerkResolverTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Morgott.PerkOracle;
using Xunit;

namespace Morgott.PerkOracle.Tests
{
    /// <summary>
    /// Unit tests for the pure class-perk core. Defs are faked with strings ("name" -> "DEF:name")
    /// so the ordering/dedup/skip logic is exercised without Unity/TFTV. Mirrors PerkPoolResolverTests.
    /// </summary>
    public class ClassPerkResolverTests
    {
        // name -> def fake: every name resolves to "DEF:" + name, except names containing "MISSING".
        private static readonly Func<string, string> Resolve =
            name => name.Contains("MISSING") ? null : "DEF:" + name;

        [Fact]
        public void OrderedNames_ResolveInOrder()
        {
            var raw = new List<string> { "PROFICIENCY", "PERK A", "PERK B" };
            var result = ClassPerkResolver.Resolve(raw, Resolve);
            Assert.Equal(new[] { "DEF:PROFICIENCY", "DEF:PERK A", "DEF:PERK B" }, result);
        }

        [Fact]
        public void DuplicateNames_AreDeduped()
        {
            var raw = new List<string> { "PERK A", "PERK A", "PERK B" };
            var result = ClassPerkResolver.Resolve(raw, Resolve);
            Assert.Equal(new[] { "DEF:PERK A", "DEF:PERK B" }, result);
        }

        [Fact]
        public void ResolverMiss_NamesAreSkipped()
        {
            var raw = new List<string> { "PERK A", "MISSING ONE", "PERK B" };
            var result = ClassPerkResolver.Resolve(raw, Resolve);
            Assert.Equal(new[] { "DEF:PERK A", "DEF:PERK B" }, result);
        }

        [Fact]
        public void NullOrEmptyNamesInList_AreSkipped()
        {
            var raw = new List<string> { "PERK A", null, "", "PERK B" };
            var result = ClassPerkResolver.Resolve(raw, Resolve);
            Assert.Equal(new[] { "DEF:PERK A", "DEF:PERK B" }, result);
        }

        [Fact]
        public void NullRawNames_ReturnsEmpty()
        {
            var result = ClassPerkResolver.Resolve(null, Resolve);
            Assert.Empty(result);
        }

        [Fact]
        public void NullResolver_ReturnsEmpty()
        {
            var raw = new List<string> { "PERK A" };
            var result = ClassPerkResolver.Resolve<string>(raw, null);
            Assert.Empty(result);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PerkOracle.Tests/PerkOracle.Tests.csproj --nologo`
Expected: FAIL — build error `The name 'ClassPerkResolver' does not exist in the current context` (the type is not created yet).

- [ ] **Step 3: Write minimal implementation**

Create `src/ClassPerkResolver.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Pure ordering/dedup/resolve core for a subclass's guaranteed class-track perks. Has no Unity,
    /// TFTV or Harmony dependency: callers inject the ordered candidate names (read from the class
    /// ability track) and a name->def resolver, so the selection logic is unit-testable with fakes.
    /// Class perks are deterministic, so unlike <see cref="PerkPoolResolver"/> there is no class
    /// exclusion filter — only ordering, de-duplication and resolver-miss skipping. The game-side
    /// wiring (reading the track, resolving defs) lives in <c>ClassPerkProvider</c>.
    /// </summary>
    public static class ClassPerkResolver
    {
        /// <summary>
        /// Resolve an ordered class-track name list into an ordered, de-duplicated list of defs,
        /// dropping empty names and names that do not resolve. Order follows <paramref name="rawNames"/>.
        /// </summary>
        /// <typeparam name="T">Def type (a real TacticalAbilityDef in game; a fake in tests).</typeparam>
        /// <param name="rawNames">Ordered candidate names for the class track; null => empty result.</param>
        /// <param name="resolve">name -> def, or default(T)/null when the name has no def.</param>
        public static List<T> Resolve<T>(
            IReadOnlyList<string> rawNames,
            Func<string, T> resolve)
        {
            var result = new List<T>();
            if (rawNames == null || resolve == null)
            {
                return result;
            }

            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in rawNames)
            {
                if (string.IsNullOrEmpty(name) || !seenNames.Add(name))
                {
                    continue;
                }

                T def = resolve(name);
                // Reference types: skip nulls. Value types never null, kept as-is.
                if (def == null)
                {
                    continue;
                }
                result.Add(def);
            }

            return result;
        }
    }
}
```

Then add the link-compile entry to `tests/PerkOracle.Tests/PerkOracle.Tests.csproj` inside the existing pure-cores `<ItemGroup>` (the one containing `PerkPoolResolver.cs`), immediately after the `PerkSwapDecision.cs` line:

```xml
    <!-- ClassPerkResolver is the pure class-track ordering/dedup core (no Unity/TFTV types). -->
    <Compile Include="..\..\src\ClassPerkResolver.cs" Link="ClassPerkResolver.cs" />
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/PerkOracle.Tests/PerkOracle.Tests.csproj --nologo`
Expected: PASS — `пройдено 40` (34 existing + 6 new) / `Passed! 40`.

- [ ] **Step 5: Commit**

```bash
git -C 'E:\DEV\PhoenixPoint\PerkOracle' add src/ClassPerkResolver.cs tests/PerkOracle.Tests/ClassPerkResolverTests.cs tests/PerkOracle.Tests/PerkOracle.Tests.csproj
git -C 'E:\DEV\PhoenixPoint\PerkOracle' commit -m "feat(PerkOracle): add pure ClassPerkResolver core with unit tests"
```

---

## Task 2 — Localization term `PERKORACLE_WIKI_TITLE_CLASS`

**Files:**
- Modify: `Assets/Localization/PerkOracle_Localization.csv`

The CSV column order is **fixed** as `Key,English,Chinese (Simplified),French,German,Italian,Polish,Russian,Spanish` (header is line 1). Append the new term as a final row. There is no class-exclusion logic to test here; verification is a CSV-shape check.

- [ ] **Step 1: Add the new CSV row**

Append exactly this single line to the end of `Assets/Localization/PerkOracle_Localization.csv` (file already ends with a trailing newline, so this becomes a new final line). Author translations of "CLASS PERKS":

```text
PERKORACLE_WIKI_TITLE_CLASS,CLASS PERKS,职业天赋,PERKS DE CLASSE,KLASSEN-PERKS,PERK DI CLASSE,PERKI KLASY,КЛАССОВЫЕ ПЕРКИ,PERKS DE CLASE
```

- [ ] **Step 2: Verify the row is well-formed**

Run: `dotnet tool run dotnet-csv --help 2>$null; (Get-Content 'Assets/Localization/PerkOracle_Localization.csv' | Select-String 'PERKORACLE_WIKI_TITLE_CLASS')`
Simpler verification (PowerShell): confirm the new row has exactly 9 comma-separated fields and matches the header column count:

Run:
```powershell
$h = (Get-Content 'Assets/Localization/PerkOracle_Localization.csv' -TotalCount 1) -split ',' ;
$r = (Get-Content 'Assets/Localization/PerkOracle_Localization.csv' | Select-String '^PERKORACLE_WIKI_TITLE_CLASS,').Line -split ',' ;
"header=$($h.Count) row=$($r.Count)"
```
Expected: `header=9 row=9`

- [ ] **Step 3: Commit**

```bash
git -C 'E:\DEV\PhoenixPoint\PerkOracle' add Assets/Localization/PerkOracle_Localization.csv
git -C 'E:\DEV\PhoenixPoint\PerkOracle' commit -m "feat(PerkOracle): add PERKORACLE_WIKI_TITLE_CLASS loc term (8 langs)"
```

---

## Task 3 — Thread an optional title into `PerkWikiPanel.Open`

**Files:**
- Modify: `src/PerkWikiPanel.cs`

The banner title is currently hard-wired to `PERKORACLE_WIKI_TITLE` / "POSSIBLE SKILLS" (PerkWikiPanel.cs:30-31, used in `BuildTitle` at line 336). Add optional `titleTerm`/`titleFallback` params to `Open`, default them to the existing constants (so the current progression-screen caller at `OnCancelInputHandlerPatch.cs:77` keeps working unchanged), and thread them through `BuildPanel` → `BuildTitle`. This is UI code (net472 against Unity) and is not unit-tested; it is exercised by Task 4's manual in-game verification.

- [ ] **Step 1: Change the `Open` signature to accept the title**

In `src/PerkWikiPanel.cs`, replace the `Open` signature line:

```csharp
public static void Open(Canvas canvas, List<TacticalAbilityDef> defs, PerkSwapContext swapContext = null)
```

with:

```csharp
public static void Open(Canvas canvas, List<TacticalAbilityDef> defs, PerkSwapContext swapContext = null,
            string titleTerm = TitleTerm, string titleFallback = TitleFallback)
```

- [ ] **Step 2: Pass the title from `Open` into `BuildPanel`**

In `src/PerkWikiPanel.cs`, in the body of `Open`, replace the call:

```csharp
                BuildPanel(_root.transform, defs, rootCanvas, swapContext);
```

with:

```csharp
                BuildPanel(_root.transform, defs, rootCanvas, swapContext, titleTerm, titleFallback);
```

- [ ] **Step 3: Add the title params to `BuildPanel` and forward to `BuildTitle`**

In `src/PerkWikiPanel.cs`, replace the `BuildPanel` signature (currently lines 120-121):

```csharp
        private static void BuildPanel(Transform parent, List<TacticalAbilityDef> defs, Canvas rootCanvas,
            PerkSwapContext swapContext)
```

with:

```csharp
        private static void BuildPanel(Transform parent, List<TacticalAbilityDef> defs, Canvas rootCanvas,
            PerkSwapContext swapContext, string titleTerm, string titleFallback)
```

and replace the `BuildTitle` call (currently line 151):

```csharp
            BuildTitle(panelGo.transform);
```

with:

```csharp
            BuildTitle(panelGo.transform, titleTerm, titleFallback);
```

- [ ] **Step 4: Use the passed title in `BuildTitle`**

In `src/PerkWikiPanel.cs`, replace the `BuildTitle` signature (currently line 315):

```csharp
        private static void BuildTitle(Transform panel)
```

with:

```csharp
        private static void BuildTitle(Transform panel, string titleTerm, string titleFallback)
```

and replace the text assignment (currently line 336):

```csharp
                text.text = Loc.Get(TitleTerm, TitleFallback);
```

with:

```csharp
                text.text = Loc.Get(titleTerm, titleFallback);
```

- [ ] **Step 5: Build the mod to verify it compiles**

Run: `dotnet build PerkOracle.csproj -c Release --nologo`
Expected: `Build succeeded` with 0 errors. (Builds against the ModSDK + game Managed DLLs declared in `PerkOracle.csproj`.)

- [ ] **Step 6: Commit**

```bash
git -C 'E:\DEV\PhoenixPoint\PerkOracle' add src/PerkWikiPanel.cs
git -C 'E:\DEV\PhoenixPoint\PerkOracle' commit -m "feat(PerkOracle): allow a custom title term/fallback in PerkWikiPanel.Open"
```

---

## Task 4 — `ClassPerkProvider` game-side adapter

**Files:**
- Create: `src/ClassPerkProvider.cs`

This is the Unity-touching adapter: it reads the class ability track off a `SpecializationDef`, hands the ordered names to the pure `ClassPerkResolver.Resolve`, and resolves names back to defs. It also computes the omitted-subclass set from `DefRepository`. Anchors (verified in spike):
- `SpecializationDef.AbilityTrack` (an `AbilityTrackDef`); `AbilityTrackDef.AbilitiesByLevel` is an `AbilityTrackSlot[]`, each `AbilityTrackSlot.Ability` is a `TacticalAbilityDef`. (`SpecializationDef.cs:27`, `AbilityTrackDef.cs:14`.)
- Full subclass set: `GameUtl.GameComponent<DefRepository>().GetAllDefs<SpecializationDef>()` — same enumeration idiom already used in `TftvConfigBridge.cs:293`.

Because this is pure-but-Unity-typed glue (it dereferences real def objects), it is not unit-tested under net8; the ordering/dedup it relies on is already covered by Task 1, and its in-game behavior is verified manually in Task 6.

- [ ] **Step 1: Write the implementation**

Create `src/ClassPerkProvider.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Base.Core;
using Base.Defs;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Game-side adapter that turns a subclass <see cref="SpecializationDef"/> into the ordered,
    /// de-duplicated list of its guaranteed class-track perks, and enumerates the full subclass set
    /// so the picker patch can grey-inject the subclasses the screen omitted. The pure ordering/dedup
    /// lives in <see cref="ClassPerkResolver"/>; this class only reads the game's defs and resolves
    /// names. Every public method is guarded so a failure never breaks the host screen.
    /// </summary>
    public static class ClassPerkProvider
    {
        /// <summary>
        /// Ordered, de-duplicated guaranteed class-track perks for <paramref name="spec"/> (level order:
        /// the spec proficiency first, then each ability slot). Returns an empty list on null/missing
        /// inputs or any error. The result feeds <see cref="PerkWikiPanel.Open"/> directly.
        /// </summary>
        public static List<TacticalAbilityDef> GetClassPerks(SpecializationDef spec)
        {
            try
            {
                if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)spec.AbilityTrack == (UnityEngine.Object)null
                    || spec.AbilityTrack.AbilitiesByLevel == null)
                {
                    return new List<TacticalAbilityDef>();
                }

                // Build the ordered ability list directly from the class track. Proficiency first
                // (matches SpecializationDef.GetAbilitiesTillLevel), then each level slot's ability.
                var ordered = new List<TacticalAbilityDef>();
                ClassProficiencyAbilityDef prof = spec.GetSpecProficiency();
                if ((UnityEngine.Object)(object)prof != (UnityEngine.Object)null)
                {
                    ordered.Add(prof);
                }
                foreach (AbilityTrackSlot slot in spec.AbilityTrack.AbilitiesByLevel)
                {
                    if (slot != null && (UnityEngine.Object)(object)slot.Ability != (UnityEngine.Object)null)
                    {
                        ordered.Add(slot.Ability);
                    }
                }

                // Index the ordered defs by name, then run the pure resolver over the name order so the
                // dedup/skip logic stays in the tested core. name -> def via this local map.
                var byName = new Dictionary<string, TacticalAbilityDef>(StringComparer.Ordinal);
                var names = new List<string>(ordered.Count);
                foreach (TacticalAbilityDef def in ordered)
                {
                    string n = ((UnityEngine.Object)def).name;
                    if (string.IsNullOrEmpty(n))
                    {
                        continue;
                    }
                    if (!byName.ContainsKey(n))
                    {
                        byName[n] = def;
                    }
                    names.Add(n);
                }

                return ClassPerkResolver.Resolve(names,
                    n => byName.TryGetValue(n, out TacticalAbilityDef d) ? d : null);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] ClassPerkProvider.GetClassPerks failed: " + ex.Message);
                return new List<TacticalAbilityDef>();
            }
        }

        /// <summary>
        /// Subclasses present in the full game def set but NOT in <paramref name="shown"/> (the specs the
        /// picker actually displays). These are the "greyed/unresearched" entries to inject. Compared by
        /// reference. Returns an empty list on any error.
        /// </summary>
        public static List<SpecializationDef> GetOmittedSubclasses(IEnumerable<SpecializationDef> shown)
        {
            try
            {
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                if (repo == null)
                {
                    return new List<SpecializationDef>();
                }

                var shownSet = new HashSet<SpecializationDef>(shown ?? Enumerable.Empty<SpecializationDef>());
                var omitted = new List<SpecializationDef>();
                foreach (SpecializationDef spec in repo.GetAllDefs<SpecializationDef>())
                {
                    if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null || shownSet.Contains(spec))
                    {
                        continue;
                    }
                    // Only real, selectable subclasses: must have a class track and a proficiency, and
                    // be usable as a second class (mirrors the picker's own filtering intent).
                    if ((UnityEngine.Object)(object)spec.AbilityTrack == (UnityEngine.Object)null
                        || (UnityEngine.Object)(object)spec.GetSpecProficiency() == (UnityEngine.Object)null
                        || spec.NotSecondClassSpecialization)
                    {
                        continue;
                    }
                    omitted.Add(spec);
                }
                return omitted;
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] ClassPerkProvider.GetOmittedSubclasses failed: " + ex.Message);
                return new List<SpecializationDef>();
            }
        }
    }
}
```

- [ ] **Step 2: Build the mod to verify it compiles**

Run: `dotnet build PerkOracle.csproj -c Release --nologo`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git -C 'E:\DEV\PhoenixPoint\PerkOracle' add src/ClassPerkProvider.cs
git -C 'E:\DEV\PhoenixPoint\PerkOracle' commit -m "feat(PerkOracle): add ClassPerkProvider game-side class-track adapter"
```

---

## Task 5 — `SubclassWikiClickHandler` right-click MonoBehaviour

**Files:**
- Create: `src/SubclassWikiClickHandler.cs`

A tiny `MonoBehaviour` attached to each subclass button. On **right-click** it opens the view-only "CLASS PERKS" wiki for its `SpecializationDef`. Mirrors the right-click convention and the full try/catch idiom of `WikiAbilityTooltipTrigger` (src/WikiAbilityTooltipTrigger.cs) and `OnPointerClick`'s button-filter at WikiAbilityTooltipTrigger.cs:77. The title term/fallback constants live here so the patch and the handler agree. UI code — verified manually in Task 6, not unit-tested.

- [ ] **Step 1: Write the implementation**

Create `src/SubclassWikiClickHandler.cs`:

```csharp
using System;
using System.Collections.Generic;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Attached to a subclass picker button (available OR greyed-injected). On right-click it opens the
    /// view-only "CLASS PERKS" wiki banner for this button's <see cref="SpecializationDef"/>. Left-clicks
    /// are ignored so the native "select this subclass" action is untouched on available buttons; greyed
    /// buttons are non-selectable, so only the preview applies. Wrapped so a UI hiccup never throws into
    /// the event system. Reuses the right-click-to-open convention from the progression screen.
    /// </summary>
    public sealed class SubclassWikiClickHandler : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>I2 term + English fallback for the class-perk banner title.</summary>
        public const string ClassTitleTerm = "PERKORACLE_WIKI_TITLE_CLASS";
        public const string ClassTitleFallback = "CLASS PERKS";

        /// <summary>The subclass whose guaranteed perks this button previews.</summary>
        public SpecializationDef Spec;

        public void OnPointerClick(PointerEventData eventData)
        {
            try
            {
                if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
                {
                    return; // only right-click opens the preview; left-click stays native
                }
                if ((UnityEngine.Object)(object)Spec == (UnityEngine.Object)null)
                {
                    return;
                }

                // Wiki already open -> right-click toggles it closed (matches the progression screen).
                if (PerkWikiPanel.IsOpen)
                {
                    PerkWikiPanel.Close();
                    return;
                }

                List<TacticalAbilityDef> defs = ClassPerkProvider.GetClassPerks(Spec);
                if (defs == null || defs.Count == 0)
                {
                    Debug.Log("[PerkOracle] subclass wiki: empty class-perk list for "
                              + ((UnityEngine.Object)Spec).name);
                    return;
                }

                Canvas canvas = ((Component)this).GetComponentInParent<Canvas>();
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null)
                {
                    return;
                }

                // View-only: swapContext = null. Custom title => "CLASS PERKS".
                PerkWikiPanel.Open(canvas, defs, null, ClassTitleTerm, ClassTitleFallback);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassWikiClickHandler.OnPointerClick failed: " + ex.Message);
            }
        }
    }
}
```

- [ ] **Step 2: Build the mod to verify it compiles**

Run: `dotnet build PerkOracle.csproj -c Release --nologo`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git -C 'E:\DEV\PhoenixPoint\PerkOracle' add src/SubclassWikiClickHandler.cs
git -C 'E:\DEV\PhoenixPoint\PerkOracle' commit -m "feat(PerkOracle): add SubclassWikiClickHandler right-click preview hook"
```

---

## Task 6 — Harmony POSTFIX: wire handlers + grey-inject omitted subclasses

**Files:**
- Create: `src/SelectSpecializationDataBindPatch.cs`

POSTFIX on `SelectSpecializationDataBind.ModalShowHandler` (the populate seam, SelectSpecializationDataBind.cs:28). After the native code activates one button per `_data.AvailableSpecs[i]` and `SetActive(false)`s the rest, the postfix:
1. attaches a `SubclassWikiClickHandler` (with `.Spec`) to every **active** button so available subclasses are right-clickable;
2. clones one active button per **omitted** subclass (`ClassPerkProvider.GetOmittedSubclasses`), greys it, makes it non-selectable (no native select listener), runs the native `InitSpecialization` so it looks like a real entry, and attaches a `SubclassWikiClickHandler` so it is right-clickable too.

Anchors: container `DualClassButtonContainer` (Transform, line 18); per-element `SpecializationOptionElementController` with `.SpecializationDef`, `.ClassIcon`, `.ClassTitleLabel`, `.ClassDescriptionLabel`, `.InitSpecialization(SpecializationDef)` (SpecializationOptionElementController.cs:10-27). The shown-spec set is read from the active buttons (not the private `_data.AvailableSpecs`), so no reflection is needed. This is the riskiest, UI-touching task; the logic is kept thin (the testable core carries the weight) and verified manually in-game.

- [ ] **Step 1: Write the implementation**

Create `src/SelectSpecializationDataBindPatch.cs`:

```csharp
using System;
using System.Collections.Generic;
using HarmonyLib;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Modal;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// POSTFIX on the level-up subclass picker's populate seam. After the native code shows one button
    /// per available subclass and hides the spare slots, this:
    ///   1. makes every active subclass button right-clickable (preview its class perks);
    ///   2. injects greyed, non-selectable clones for the subclasses the screen omitted (unresearched),
    ///      also right-clickable for preview.
    /// Fully guarded so it can never break the picker; on any failure the native screen is untouched.
    /// Targets the VANILLA types (TFTV does not patch this modal). See spec 2026-06-08 Feature A.
    /// </summary>
    [HarmonyPatch(typeof(SelectSpecializationDataBind), "ModalShowHandler")]
    internal static class SelectSpecializationDataBindPatch
    {
        private static void Postfix(SelectSpecializationDataBind __instance, Transform ___DualClassButtonContainer)
        {
            try
            {
                if ((UnityEngine.Object)(object)___DualClassButtonContainer == (UnityEngine.Object)null)
                {
                    return;
                }

                SpecializationOptionElementController[] elements =
                    ((Component)___DualClassButtonContainer)
                        .GetComponentsInChildren<SpecializationOptionElementController>(true);

                // 1) Right-clickify every currently ACTIVE button + collect the shown specs.
                var shown = new List<SpecializationDef>();
                SpecializationOptionElementController activeTemplate = null;
                foreach (SpecializationOptionElementController el in elements)
                {
                    if ((UnityEngine.Object)(object)el == (UnityEngine.Object)null
                        || !((Component)el).gameObject.activeSelf)
                    {
                        continue;
                    }
                    if ((UnityEngine.Object)(object)el.SpecializationDef != (UnityEngine.Object)null)
                    {
                        shown.Add(el.SpecializationDef);
                    }
                    AttachHandler(((Component)el).gameObject, el.SpecializationDef);
                    if (activeTemplate == null)
                    {
                        activeTemplate = el; // a live, populated button to clone for greyed entries
                    }
                }

                // 2) Inject greyed clones for omitted subclasses. Need a live template to clone.
                if (activeTemplate == null)
                {
                    return; // nothing populated to clone from; leave the screen as-is
                }
                List<SpecializationDef> omitted = ClassPerkProvider.GetOmittedSubclasses(shown);
                foreach (SpecializationDef spec in omitted)
                {
                    InjectGreyedEntry(activeTemplate, ___DualClassButtonContainer, spec);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SelectSpecializationDataBind postfix failed: " + ex.Message);
            }
        }

        /// <summary>Attach (or refresh) the right-click preview handler with the button's spec.</summary>
        private static void AttachHandler(GameObject go, SpecializationDef spec)
        {
            var handler = go.GetComponent<SubclassWikiClickHandler>();
            if ((UnityEngine.Object)(object)handler == (UnityEngine.Object)null)
            {
                handler = go.AddComponent<SubclassWikiClickHandler>();
            }
            handler.Spec = spec;
        }

        /// <summary>
        /// Clone the native button <paramref name="template"/>, populate it for <paramref name="spec"/>
        /// via the native InitSpecialization, grey it out, strip its select affordance, and make it
        /// right-clickable for preview. Non-fatal: a single bad clone is logged and skipped.
        /// </summary>
        private static void InjectGreyedEntry(SpecializationOptionElementController template,
            Transform container, SpecializationDef spec)
        {
            try
            {
                if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)spec.GetSpecProficiency() == (UnityEngine.Object)null)
                {
                    return; // InitSpecialization dereferences the proficiency view element
                }

                GameObject cloneGo = UnityEngine.Object.Instantiate(
                    ((Component)template).gameObject, container, false);
                cloneGo.name = "PerkOracleGreyedSubclass";
                cloneGo.SetActive(true);

                var cloneEl = cloneGo.GetComponent<SpecializationOptionElementController>();
                if ((UnityEngine.Object)(object)cloneEl == (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(cloneGo);
                    return;
                }

                // Populate with the native path so icon/title/description look like a real entry.
                cloneEl.InitSpecialization(spec);

                // Non-selectable: remove the native button's click listeners + disable interactability so
                // a greyed (unresearched) class can never be picked. Right-click preview still works via
                // our handler (which listens at the EventSystem level, not the Button).
                var btn = cloneGo.GetComponent<Button>();
                if ((UnityEngine.Object)(object)btn != (UnityEngine.Object)null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.interactable = false;
                }

                // Grey tint: dim every Graphic (icon + labels) so it reads as inactive.
                foreach (Graphic g in cloneGo.GetComponentsInChildren<Graphic>(true))
                {
                    Color c = g.color;
                    g.color = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, c.a * 0.6f);
                }

                AttachHandler(cloneGo, spec);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] InjectGreyedEntry failed for "
                          + ((UnityEngine.Object)(object)spec != (UnityEngine.Object)null
                             ? ((UnityEngine.Object)spec).name : "<null>")
                          + ": " + ex.Message);
            }
        }
    }
}
```

> **Note on `___DualClassButtonContainer`:** Harmony injects the public field `DualClassButtonContainer` (SelectSpecializationDataBind.cs:18) by triple-underscore name. The implementation deliberately derives the shown-spec list from the **active buttons** rather than reading the private `_data.AvailableSpecs` — this reads what is actually displayed, which is more robust against TFTV's input-list trimming (per the spec caveat) and avoids reflection.

- [ ] **Step 2: Build the mod to verify it compiles**

Run: `dotnet build PerkOracle.csproj -c Release --nologo`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Re-run unit tests (regression guard)**

Run: `dotnet test tests/PerkOracle.Tests/PerkOracle.Tests.csproj --nologo`
Expected: PASS — `Passed! 40` (no pure-core regressions).

- [ ] **Step 4: Deploy + manual in-game verification**

Run: `pwsh -File deploy.ps1` (deploys the built mod to the game per the repo's deploy script).

Then in-game (author-run; this UI/Harmony task cannot be unit-tested):
1. Start/load a save; level a soldier to the point where the **Add Subclass** (dual-class) picker opens.
2. **Available subclass — right-click:** right-click an available subclass button → the "CLASS PERKS" banner opens listing that subclass's proficiency + class-track perks with native icons/tooltips. Right-click again (or click the backdrop) → it closes. Confirm the title reads "CLASS PERKS" (localized).
3. **Left-click still selects:** left-click an available subclass → the native selection/confirm still works (the right-click hook did not break it).
4. **Greyed injection:** confirm unresearched subclasses appear as greyed, dimmed buttons; they are **not** selectable (left-click does nothing) but **are** right-clickable → their "CLASS PERKS" banner opens.
5. **Fail-safe:** confirm the picker opens and behaves normally even if PerkOracle logged any `[PerkOracle] ...` warning (the screen must never break).
6. Check the player log for any `[PerkOracle] ... failed:` lines and resolve before shipping.

- [ ] **Step 5: Commit**

```bash
git -C 'E:\DEV\PhoenixPoint\PerkOracle' add src/SelectSpecializationDataBindPatch.cs
git -C 'E:\DEV\PhoenixPoint\PerkOracle' commit -m "feat(PerkOracle): right-click subclass perk preview + greyed unresearched entries"
```

---

## Self-Review (run by the plan author)

**1. Spec coverage (Feature A scope items 1-5):**
- (1) Pure `ClassPerkResolver` mirroring `PerkPoolResolver`, injected resolver, unit-tested with fakes, no Unity dep → **Task 1**. ✔
- (2) Right-click hook on `SpecializationOptionElementController` (available or greyed) → `PerkWikiPanel.Open(canvas, defs, null, ...)` view-only, right-click open/close convention → **Tasks 5 + 6**. ✔
- (3) Greyed-entry injection: enumerate full subclass set from `DefRepository`, inject omitted ones as greyed non-selectable native clones, still right-clickable → **Tasks 4 (`GetOmittedSubclasses`) + 6 (`InjectGreyedEntry`)**. ✔
- (4) New I2 loc term `PERKORACLE_WIKI_TITLE_CLASS` ("CLASS PERKS") in all 8 languages → **Task 2** (+ title plumbing in **Task 3**). ✔
- (5) Rolled-cell highlight + candidate banner ride along for free, no new code → noted in **File Structure note**; no task re-implements it. ✔
- All bodies try/catch + `Debug.Log("[PerkOracle] ...")`; read-only/additive/fail-safe; clone native element (no hand-built UGUI) → satisfied in Tasks 4-6. ✔
- Ordering: pure tested core (Task 1) first; UI/Harmony (Tasks 4-6) after, with manual in-game verification steps (Task 6 Step 4) since they touch Unity/Harmony. ✔

**2. Placeholder scan:** No "TBD/TODO/add error handling/similar to Task N". Every code step contains complete code; every command has expected output; every commit message is spelled out (Conventional Commits, `feat(PerkOracle): ...` / one `docs:` for the plan commit itself). ✔

**3. Type consistency:**
- `ClassPerkResolver.Resolve<T>(IReadOnlyList<string>, Func<string,T>)` — same signature in Task 1 impl, Task 1 tests, and Task 4 caller. ✔
- `ClassPerkProvider.GetClassPerks(SpecializationDef)` / `GetOmittedSubclasses(IEnumerable<SpecializationDef>)` — defined in Task 4, called identically in Tasks 5 and 6. ✔
- `PerkWikiPanel.Open(Canvas, List<TacticalAbilityDef>, PerkSwapContext=null, string titleTerm=TitleTerm, string titleFallback=TitleFallback)` — defined in Task 3, called with `(canvas, defs, null, ClassTitleTerm, ClassTitleFallback)` in Task 5. ✔
- `SubclassWikiClickHandler.Spec` / `.ClassTitleTerm` / `.ClassTitleFallback` — defined in Task 5, set/used in Task 6. ✔
- Decompile member names (`SpecializationDef.AbilityTrack`, `AbilityTrackDef.AbilitiesByLevel`, `AbilityTrackSlot.Ability`, `SpecializationOptionElementController.{SpecializationDef,InitSpecialization}`, `SelectSpecializationDataBind.{ModalShowHandler,DualClassButtonContainer,_data}`) match the verified spike anchors. ✔
