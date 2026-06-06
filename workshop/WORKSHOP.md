# PerkOracle — Steam Workshop publishing playbook

End-to-end guide for publishing and updating PerkOracle on the Steam Workshop
for **Phoenix Point** (appid **839770**).

Each step is tagged:
- **[NEEDS YOU]** — requires Steam login / a GUI; only you can do it.
- **[AUTOMATED]** — a script in this repo does it.

---

## 0. What gets uploaded

A Phoenix Point Workshop item is just a plain folder containing:

```
PerkOracle.dll
meta.json
Assets/
```

No special packaging. The `workshop/pack-dist.ps1` script assembles exactly this
into `workshop/Dist/` (gitignored). `meta.json` already matches the required PP
schema (Id/AssemblyName/Version/Dependencies + localized Author/Name/Description
arrays).

---

## 1. First publish — via PPWorkshopTool  **[NEEDS YOU]**

There is **no CLI** for the very first publish on Phoenix Point. Use the official
GUI to create the item and get its `publishedfileid`.

1. Make sure the **Steam client is running** and the account **owns Phoenix Point**.
2. Download **PPWorkshopTool** from SnapshotGames:
   <https://github.com/SnapshotGames/PPWorkshopTool> (Windows GUI).
3. Build the content folder first **[AUTOMATED]**:
   ```powershell
   ./workshop/pack-dist.ps1
   ```
   This produces `workshop/Dist/` (DLL + meta.json + Assets/).
4. In PPWorkshopTool: **create a New Workshop Item**, point it at
   `workshop/Dist/`, set the **title** (`PerkOracle`) and **preview image**
   (`image/steam_preview.jpg`, must be ≤ 1 MB), and **upload**.
5. **Find the publishedfileid:**
   - In the Workshop item URL after upload: `...?id=<publishedfileid>`.
   - And/or shown in PPWorkshopTool itself.

PPWorkshopTool uses Facepunch.Steamworks / ISteamUGC — the **same backend** as
SteamCMD's `workshop_build_item`, so future updates can use SteamCMD.

---

## 2. Record the publishedfileid  **[NEEDS YOU — one edit]**

Open `workshop/perkoracle.vdf` and replace the placeholder:

```
"publishedfileid" "PUBLISHEDFILEID_PLACEHOLDER"
```
with the real id, e.g.:
```
"publishedfileid" "1234567890"
```

(`0` = create a new item; a real id = update that item. We already created it in
step 1, so always use the real id from here on.)

---

## 3. Updates — via SteamCMD / update.ps1  **[NEEDS YOU to log in, AUTOMATED otherwise]**

All future updates use the **local main-account path** (the maintainer's choice):

```powershell
./workshop/update.ps1 -ChangeNote "What changed in this update" -SteamUser <yoursteamname>
```

`update.ps1`:
- verifies SteamCMD is installed (prints an install hint if not),
- refuses to run if the vdf still has the placeholder id,
- runs `pack-dist.ps1` to rebuild `Dist/`,
- stamps the change note into the vdf,
- runs `steamcmd +login <user> +workshop_build_item <abs vdf> +quit`.

SteamCMD prompts for your **password + Steam Guard / 2FA** in its own console.
Credentials are **never** stored by the script. You must be logged in as the
item's **owner** account.

> Install SteamCMD if needed: download
> <https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip>, extract to
> `C:\steamcmd`, then re-run (or pass `-SteamCmd C:\steamcmd\steamcmd.exe`).

Tip: do one manual SteamCMD update first to confirm login/2FA works, then rely on
`update.ps1` thereafter.

---

## 4. Store page content  **[NEEDS YOU]**

On the Workshop item's **Edit** page:

1. **Description** — paste the BBCode from:
   - English: `workshop/description_en.txt`
   - Russian: `workshop/description_ru.txt`
   (Steam descriptions use **BBCode**, not Markdown. `[color]` is not supported.)
2. **Preview image** — upload `image/steam_preview.jpg` (≤ 1 MB).
3. **Gallery screenshots** — add:
   - `image/screenshot1.jpg` — the ability-progression grid with rolled cells
     highlighted (distinct hatched background vs. fixed/class perks).
   - `image/screenshot2.jpg` — the candidate wiki popup ("Possible Perks")
     listing every perk that could roll into the selected slot.
4. Set **tags / required items** as appropriate (note the dependency: TFTV).

---

## 5. GitHub social preview  **[NEEDS YOU — web only]**

Upload `image/github_social.png` at:
**GitHub repo → Settings → Social preview**
(<https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/settings>). This is
web-only; there is no API/CLI for it.

---

## 6. Comments workflow  **[AUTOMATED read / NEEDS YOU to post]**

- **Read** comments (no login):
  ```powershell
  pip install -r workshop/comments/requirements.txt
  python workshop/comments/read_comments.py --owner <YourSteamID64> --item <publishedfileid> --count 50
  ```
  (Undocumented endpoint — may change without notice.)
- **Draft** EN/RU replies: see `workshop/comments/draft_replies.md`.
- **Post**: do it **manually in the browser** (recommended). The
  `post_comment.py` writer is **experimental, unofficial, ToS-violating, and
  carries account-ban risk** (Steam's flow broke at Valve's 2023-10-17 update).
  It refuses to run without `--i-understand-the-risk` and reads cookies only from
  env vars.

---

## 7. (Optional) CI auto-upload  **[opt-in]**

`.github/workflows/steam-workshop.yml` can build `Dist/` and upload via SteamCMD
on `release: published`. **Not recommended by default** — CI on the main account
risks Steam Guard lockouts. The local `update.ps1` is the recommended path. See
the header comment in that workflow for required secrets.

---

## File map

| Path | Purpose |
|---|---|
| `workshop/pack-dist.ps1` | Build Release + assemble `Dist/` (DLL+meta+Assets). |
| `workshop/perkoracle.vdf` | SteamCMD build descriptor (set publishedfileid). |
| `workshop/update.ps1` | Local update path: pack + SteamCMD upload. |
| `workshop/description_en.txt` / `_ru.txt` | BBCode store descriptions. |
| `workshop/comments/` | Read tool, reply workflow, experimental writer. |
| `image/steam_preview.jpg` | Workshop preview (≤ 1 MB). |
| `image/screenshot1.jpg` / `screenshot2.jpg` | Gallery images. |
| `image/github_social.png` | GitHub Settings → Social preview. |
| `.github/workflows/steam-workshop.yml` | Opt-in CI upload. |
