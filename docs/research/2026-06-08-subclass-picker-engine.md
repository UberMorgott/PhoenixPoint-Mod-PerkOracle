# Research note — Dual-class subclass picker & native confirm-dialog engine

> Reusable PP engine findings gathered while shipping Feature A (subclass perk preview + confirm).
> Compressed bullets, with `type` / `path:line` anchors. Verified against the game decompile
> (`decompiled/AssemblyCSharp/...`) and the shipped mod source under `src/`.

## Dual-class (subclass) picker

- **Modal:** `ModalType.DualClassPicker` — opened from `UIStateEditSoldier.OnSelectSecondaryClass`.
- **Controller / populate seam:** `SelectSpecializationDataBind` — `ModalShowHandler` is the
  **synchronous** populate seam (shows one button per available subclass, hides spare slots from a
  **fixed button pool** in `DualClassButtonContainer`). Postfix here to inject greyed clones.
  @ `PhoenixPoint.Geoscape.View.ViewControllers.Modal\SelectSpecializationDataBind.cs`.
- **Per-subclass element:** `SpecializationOptionElementController` — carries `.SpecializationDef`;
  populate a clone via the native `InitSpecialization(spec)` (it dereferences the proficiency view
  element, so guard `spec.GetSpecProficiency() != null` before calling).
  @ `...ViewControllers\SpecializationOptionElementController.cs`.
- **Native select applier:** `SelectSpecializationDataBind.SelectSpecializationElement(element)` —
  sets `_data.SelectedSpec` + calls `_modal.Confirm()`. Each active button's prefab `onClick` invokes it.
  Prefix this to intercept the select (return false to suppress; re-invoke with a one-shot guard to let
  YES through). See `src/SelectSpecializationConfirmPatch.cs`.
- **Pool reuse caveat:** the container is **persistent and reused** across level-ups (`_shown` resets on
  hide); injected clones survive. Destroy stale clones with `DestroyImmediate` **before** the same-frame
  component scan so they can't be re-picked. See `src/SelectSpecializationDataBindPatch.cs:97`.

## Input routing inside a geoscape modal

- **RMB / Esc cancel → `UIStateGeoModal.OnCancel`** (closes the modal via `FinishQueriedState`). Inside
  this modal **RMB is NEVER delivered as a UGUI pointer-click** — only `button=Left` arrives at an
  `IPointerClickHandler` (confirmed by runtime diag). So a modal-local preview gesture must be **left-click**,
  not right-click.
- **Two-stage cancel pattern:** prefix `UIStateGeoModal.OnCancel`; if our overlay is open, `Close()` it and
  return false (swallow the cancel, keep the modal). Postfix `UIStateGeoModal.ExitState` as an orphan
  fail-safe — tear down any still-open overlay whenever the modal closes by any route.
  See `src/SelectSpecializationCancelPatch.cs`.

## Native yes/no prompt

- `GameUtl.GetMessageBox()` → `MessageBox`; `box.ShowSimplePrompt(text, MessageBoxIcon, MessageBoxButtons.YesNo,
  callback, sender, userData)`. Returns null early in startup — null-guard it. `userData` survives onto the
  shown `ModalData` and is the clean way to tag "this is our prompt".
- **Reading the prompt back (decorator):** postfix `MessageBoxPromptController.Show`; the live controller is
  `__instance`. `MessageBox.ModalData` is internal — read `_shownData` (private field on the controller) via
  reflection, then its `UserData` field. `controller.TextContent` is the question label. See
  `src/SubclassConfirmPopupDecorator.cs:47`. @ `Base.UI.MessageBox.PromptControllers\MessageBoxPromptController.cs`.

## Confirm-dialog layout hierarchy (measured)

- Hierarchy: **`Dialog`** (center-anchored `VerticalLayoutGroup` + `ContentSizeFitter`(v=PreferredSize) —
  stacks children top→bottom and **auto-grows** height) → **`Content`** (`HorizontalLayoutGroup` — a plain
  sibling row would land to the RIGHT of the text) → **`Snapshot Text`** (the question).
- To inject a perk-icon row **above** the question: add it as the **first child of the `Dialog` VLG**
  (`SetSiblingIndex(0)`) with a `LayoutElement` reserving its height. No manual resizing (that would fight the
  `ContentSizeFitter`). Find `Dialog` = nearest ancestor of the text with a `VerticalLayoutGroup`.
- **Sorting (measured z-order):** the dialog/`SystemMessageCanvas` sorts at **sortingOrder 130**. A row added
  as a normal child renders **behind** the dialog scrim and stops receiving raycasts. Fix = give the row its
  **own** `Canvas` with `overrideSorting = true` + a higher `sortingOrder`, **plus its own `GraphicRaycaster`**
  (the nested override canvas needs one to be hit again). Shipped chain: dialog bg (130) < icons (180,
  `dialogSortingOrder + 50`) < tooltip (230, `dialogSortingOrder + 100`). The tooltip clone uses
  `overrideSorting` + a `CanvasGroup`(blocksRaycasts=false) so it draws above but stays non-interactive.
- Tooltip clone source: `GeoRosterAbilityDetailTooltip` (same native framed ability tooltip the wiki uses);
  clone per-popup, parent to the **root canvas**, tie lifetime to the row (`OnDisable`/`OnDestroy`). See
  `src/SubclassConfirmPopupDecorator.cs` (`CreateTooltip`, `ConfirmRowCleanup`).

## Subclass universe (data-driven)

- Authoritative player second-class set = `GeoFactionDef.InitialSpecializationDefs` ∪ every
  `ClassResearchRewardDef.SpecializationDef` (exactly what `GeoFaction.AddSpecialization` ever receives).
  This excludes non-player specs cleanly; `GetAllDefs<SpecializationDef>()` does not (pulls in
  Raider/Mutoid/Scum/Slug). Filter out `NotSecondClassSpecialization` / no-`AbilityTrack` / no-proficiency.
  See `src/ClassPerkProvider.cs` (`GetSelectableSubclassUniverse`, `GetOmittedSubclasses`).
