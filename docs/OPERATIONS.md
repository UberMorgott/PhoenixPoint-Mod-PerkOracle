# PerkOracle — Agent Operations Runbook

Agent-facing runbook for routine Steam Workshop operations on the **PerkOracle**
Phoenix Point mod. Every command below is verified against the actual scripts in
`workshop/`. Execute these directly for routine update / publish / description /
tags / gallery / comments tasks — **no clarifying questions needed**.

All commands assume **CWD = the mod repo root** `E:\DEV\PhoenixPoint\PerkOracle`.
(If a session starts at the outer monorepo `E:\DEV\PhoenixPoint`, prefix paths
with `PerkOracle\` or `cd` into the repo first.)

---

## Identity & paths

| Thing | Value |
|---|---|
| Workshop publishedfileid | **3739613434** |
| Steam appid (Phoenix Point) | **839770** |
| Item URL | <https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434> |
| Owner SteamID64 (Morgott) | **76561197996210591** |
| Repo path | `E:\DEV\PhoenixPoint\PerkOracle` |
| Remote (origin, branch main) | <https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle> |
| Publisher script | `workshop/steamugc/publish_ugc.py` |
| Build/pack script | `workshop/pack-dist.ps1` |
| Locale descriptions | `workshop/locale/description.<lang>.txt` (8 languages) |
| Comment reader | `workshop/comments/read_comments.py` |
| Preview image (square) | `image/steam_preview.jpg` (set headlessly; ≤ 1 MB) |
| Gallery images | `image/screenshot1.jpg`, `image/screenshot2.jpg` |

Current Workshop tags: **`["Gameplay", "Tactical"]`** (hardcoded in
`publish_ugc.py` as `WORKSHOP_TAGS`).
Locale order pushed: **english, russian, german, french, spanish, italian,
polish, schinese** (Steam shows each viewer their client-language description;
**english is the fallback**).

---

## Quick command cheat-sheet

```powershell
# UPDATE the mod (new code/build) — rebuild Dist, then upload:
pwsh -File workshop/pack-dist.ps1
python workshop/steamugc/publish_ugc.py --update --item 3739613434 --changenote "<what changed>"

# Push localized store descriptions for all 8 languages (also re-applies tags):
python workshop/steamugc/publish_ugc.py --localize-descriptions --item 3739613434 --changenote "<note>"

# READ Workshop comments (no login):
python workshop/comments/read_comments.py --owner 76561197996210591 --item 3739613434 --count 50

# After ANY change, commit + push the mod repo:
git -C E:\DEV\PhoenixPoint\PerkOracle add -A
git -C E:\DEV\PhoenixPoint\PerkOracle commit -m "<message>"
git -C E:\DEV\PhoenixPoint\PerkOracle push origin main
```

---

## Prerequisites (CRITICAL — read before any publish)

1. **Steam desktop client must be RUNNING and LOGGED IN as the owner account
   (Morgott, SteamID64 76561197996210591).** The publisher is headless and rides
   the active Steam session — **no username/password is used** (same auth model
   as the official PPWorkshopTool). If Steam is closed or logged into another
   account, the publish will bind to the wrong user or fail to initialize.

2. **Native deps in `workshop/steamugc/` are git-ignored** and must be
   re-provisioned on a fresh clone. Required local-only files:
   - `steamworks/` — the SteamworksPy python package, copied from
     <https://github.com/philippj/SteamworksPy> (the repo's `steamworks/` folder).
   - `SteamworksPy64.dll` — native ctypes shim, from that repo's
     `redist/windows/SteamworksPy64.dll` (built against Steamworks SDK 1.64).
   - `steam_api64.dll` — **must export `SteamInternal_SteamAPI_Init`**
     (Steamworks SDK ≈ 1.57 or newer). Phoenix Point's own bundled
     `steam_api64.dll` is **too old** → fails with `WinError 127`
     (missing `SteamInternal_SteamAPI_Init`). A known-working DLL was sourced
     from **Slay the Spire 2**
     (`…\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\steam_api64.dll`).
   - `steam_appid.txt` — a file containing exactly `839770` (binds the API to PP).

3. **Smoke test** the binding before a real publish:
   ```powershell
   python workshop/steamugc/init_test.py
   ```
   It should report `appid=839770` and the logged-in user. If it errors, fix the
   deps above before proceeding.

4. **Python read-comments deps** (one-time): `pip install -r workshop/comments/requirements.txt`
   (`requests`, `beautifulsoup4`).

---

## Task: Update the mod (new code / new build)

Trigger phrases: "обнови мод", "update the mod", "publish a new build".

1. Rebuild the clean content folder (`workshop/Dist/` = `PerkOracle.dll` +
   `meta.json` + `Assets/`):
   ```powershell
   pwsh -File workshop/pack-dist.ps1
   ```
   This runs `dotnet build -c Release` then assembles `Dist/`. It throws if the
   build fails — do not proceed if it errors.

2. Upload to the existing Workshop item (blocks until upload commits):
   ```powershell
   python workshop/steamugc/publish_ugc.py --update --item 3739613434 --changenote "<what changed>"
   ```
   - Sets title/description (english fallback)/content/preview/visibility, then
     `SubmitItemUpdate`. **Block until it prints `EResult.OK`** ("upload
     committed"). On failure it raises `SystemExit` with the `EResult` code.
   - If it reports the **workshop legal agreement** flag, open the item URL once
     in the Steam client / browser and accept it (one-time per account).
   - Default `--visibility` is `public`; pass `--visibility friends|private` only
     if explicitly requested.

3. Commit + push the repo:
   ```powershell
   git -C E:\DEV\PhoenixPoint\PerkOracle add -A
   git -C E:\DEV\PhoenixPoint\PerkOracle commit -m "chore(workshop): publish update — <what changed>"
   git -C E:\DEV\PhoenixPoint\PerkOracle push origin main
   ```

---

## Task: Edit / localize the store description

Trigger phrases: "поменяй описание", "update the description", "localize".

1. Edit the relevant file(s) in `workshop/locale/`:
   `description.english.txt`, `.russian.txt`, `.german.txt`, `.french.txt`,
   `.spanish.txt`, `.italian.txt`, `.polish.txt`, `.schinese.txt`.
   Rules:
   - **BBCode**, not Markdown. **No `[color]`** (unsupported by Steam).
   - Ordered lists use `[list=1] … [/list]`.
   - **Each file must stay < 8000 UTF-8 BYTES** — the publisher validates the
     *byte* length and aborts otherwise. Cyrillic/CJK chars cost ≥ 2 bytes each,
     so a Russian/Chinese file hits the limit at far fewer characters than English.

2. Push all 8 localized descriptions (this also **re-applies the tags**
   `["Gameplay","Tactical"]` on the english pass — tags are item-global):
   ```powershell
   python workshop/steamugc/publish_ugc.py --localize-descriptions --item 3739613434 --changenote "<note>"
   ```
   - This mode touches **only** per-language descriptions (+ tags on english) —
     it does NOT re-upload content, preview, title or visibility, so it resolves
     quickly per language.
   - It prints a per-language `EResult.OK` / `FAILED` table. Confirm all OK.

3. Commit + push (same git block as above).

---

## Task: Change tags

Current tags: **`["Gameplay", "Tactical"]`**.
Valid Phoenix Point Workshop tags: **Geoscape, Tactical, Difficulty, Gameplay,
Bionics, Mutations** (an unknown tag can fail the submit).

Tags are item-global and are set via `SetItemTags` during the **english pass** of
`--localize-descriptions` (the `WORKSHOP_TAGS` constant in `publish_ugc.py`).

To change them:
1. Edit `WORKSHOP_TAGS = [...]` near the top of `workshop/steamugc/publish_ugc.py`
   to the desired valid tags.
2. Re-apply by running the localize-descriptions command (it re-pushes tags):
   ```powershell
   python workshop/steamugc/publish_ugc.py --localize-descriptions --item 3739613434 --changenote "update tags"
   ```
   (Alternatively, `--update` also accepts an ad-hoc `--tags "Gameplay,Tactical"`
   comma-separated list, but it re-uploads content; prefer the localize path for a
   tags-only change.)
3. Commit + push.

---

## Task: Add / replace gallery images

The SteamworksPy build in this repo **does NOT expose `AddItemPreviewFile`**, so
gallery (screenshot) images **cannot be added headlessly** — `publish_ugc.py`
will just print them as a manual web step. Add/replace them via the **Steam web
UI on the logged-in session, using Playwright**:

1. Navigate to the manage-previews page (logged-in Steam session):
   `https://steamcommunity.com/sharedfiles/managepreviews/?id=3739613434`
2. Click **"Choose File"** and upload the image path(s), e.g.
   `E:\DEV\PhoenixPoint\PerkOracle\image\screenshot1.jpg`,
   `…\image\screenshot2.jpg`.
3. Click **"Загрузить" / Upload** for each image.
4. Click **"Сохранить и продолжить" / Save** to commit the changes.

> The **main square preview** (`image/steam_preview.jpg`) IS set headlessly by
> `publish_ugc.py` via `SetItemPreview` on every `--update` — no web step needed
> for the square preview, only for gallery screenshots.

After changing source images that live in the repo, commit + push.

---

## Task: Read & reply to Workshop comments

Trigger phrases: "ответь на комментарии", "read/reply to comments".

### Read (no login)
```powershell
python workshop/comments/read_comments.py --owner 76561197996210591 --item 3739613434 --count 50
```
Prints author + text + timestamp per comment. Uses an undocumented Steam render
endpoint (read-only; may break without notice).

### Draft
Follow the tone in `workshop/comments/draft_replies.md`: **helpful, concise,
thank reporters, stay positive.** Produce a short **EN** and short **RU** reply.
For **bug reports**, steer the reporter to:
- open a **GitHub Issue** at the repo for detailed reports, and
- attach **`Player.log`**, a **save** from just before the issue, the **mod load
  order** (confirm TFTV installed + PerkOracle loads after it), and exact repro
  steps (expected vs. observed).

Support policy: redirect technical issues to **GitHub Issues**; keep Workshop
replies brief and friendly.

### Post (no official write API)
There is **no official write API** for Workshop comments. Post replies via
**Playwright on the logged-in Steam session**:
1. Navigate to
   `https://steamcommunity.com/sharedfiles/filedetails/comments/3739613434`
   (or the item page `?id=3739613434`).
2. Find the comment text box, type the reply, submit it.

> Posting comments this way is **unofficial, fragile, and ToS-risky** (Steam's
> comment flow changed at Valve's 2023-10-17 update). The scripted alternative is
> the experimental `workshop/comments/post_comment.py`, which reads cookies from
> env vars (`STEAM_SESSIONID`, `STEAM_LOGIN_SECURE`) and refuses to run without
> `--i-understand-the-risk`. Prefer manual/Playwright posting; treat the script as
> last resort.

---

## Gotchas

- **`steam_api64.dll` version**: must export `SteamInternal_SteamAPI_Init`
  (SDK ≥ ~1.57). PP's bundled DLL is too old → `WinError 127`. Use the
  Slay-the-Spire-2 DLL (see Prerequisites).
- **8000-byte UTF-8 limit** per description file — count BYTES, not chars;
  Cyrillic/CJK = ≥ 2 bytes/char. The publisher aborts if a file exceeds it.
- **Gallery needs Playwright** — the SteamworksPy binding lacks
  `AddItemPreviewFile`; gallery screenshots are a web-only step. (Square preview
  is headless.)
- **Comment posting is web/Playwright only** — no official write API; ToS-risky.
- **Steam client must be running + logged in** as the owner for any publish; the
  publisher rides the active session (no password).
- **Native deps are git-ignored** — re-provision `steamworks/`, `SteamworksPy64.dll`,
  `steam_api64.dll`, `steam_appid.txt` on a fresh clone.
- **Always `git push origin main`** after any change (code, description, tags,
  images-in-repo).
- **Block on `EResult.OK`** — never report a publish as done until the script
  prints it.

---

## Related docs

- `workshop/WORKSHOP.md` — broader publishing playbook. NOTE: its sections on
  SteamCMD / `update.ps1` and `description_en.txt`/`description_ru.txt` describe an
  **older path**. The **current** routine path is `publish_ugc.py` +
  `workshop/locale/` as documented here; this runbook supersedes WORKSHOP.md for
  routine ops. (`workshop/update.ps1` SteamCMD upload still works as a fallback:
  `pwsh -File workshop/update.ps1 -ChangeNote "<note>" -SteamUser <name>` —
  interactive password/2FA in SteamCMD's own console.)
- `workshop/steamugc/README.md` — native-dep setup detail.
- `workshop/comments/draft_replies.md` — reply tone + reusable EN/RU snippets.

---

## Commands NOT verifiable against a script (flagged)

- **Gallery upload via Playwright** and **comment posting via Playwright** — these
  are operational web-UI steps; there is **no script** in the repo that drives
  Playwright. The page URLs and button labels above are documented from the task
  brief / Steam UI, not extracted from code. Treat them as a guide and adapt to
  the live page.
- **Owner SteamID64 `76561197996210591`** — taken from the operator (task brief);
  it is not stored in any committed script (only the item id `3739613434` lives in
  `workshop/steamugc/published_id.txt`).
