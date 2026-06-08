# PerkOracle — research notes index

Reusable engine findings about Phoenix Point internals discovered while building the mod. Each note is
compressed bullets with `type` / `path:line` anchors, grounded in the game decompile and the shipped
mod source.

- [2026-06-08-subclass-picker-engine.md](2026-06-08-subclass-picker-engine.md) — dual-class subclass
  picker (`ModalType.DualClassPicker` / `SelectSpecializationDataBind` / `SpecializationOptionElementController`),
  geoscape-modal input routing (RMB → `UIStateGeoModal.OnCancel`, only Left reaches pointer-clicks),
  native yes/no prompt (`MessageBox.ShowSimplePrompt` via `GameUtl.GetMessageBox()`), and the confirm-dialog
  layout/sorting hierarchy used to inject the perk-icon row.
