# Headless Steam Workshop publisher (SteamworksPy)

Publishes / updates the Oracle Workshop item by riding the **already-running,
logged-in Steam client** — no username or password, exactly like the official
PPWorkshopTool. Auth = your active Steam session.

Published item: **3739613434** —
<https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434>

## One-time setup

The native binaries and the SteamworksPy python package are environment-local
and **git-ignored** — recreate them in this folder (`workshop/steamugc/`):

1. **SteamworksPy** (python package + native shim) — from
   <https://github.com/philippj/SteamworksPy>:
   - Copy the repo's `steamworks/` package folder here.
   - Copy `redist/windows/SteamworksPy64.dll` here (built against Steamworks SDK 1.64).

2. **steam_api64.dll** — must export `SteamInternal_SteamAPI_Init`
   (Steamworks SDK ≈ 1.57 or newer). Phoenix Point's own
   `…\Phoenix Point\PhoenixPointWin64_Data\Plugins\x86_64\steam_api64.dll` is
   **too old** and fails to load the shim (`WinError 127`). Use a newer one,
   e.g. copied from another recent Steam game:
   `D:\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\steam_api64.dll`.
   Verify with `pefile` that it exports `SteamInternal_SteamAPI_Init`.

3. **steam_appid.txt** — a file containing exactly `839770` (no newline), so the
   API binds to Phoenix Point.

4. `pip install pefile` (only needed if you want to re-diff DLL imports).

Expected contents of this folder afterwards (★ = git-ignored / local-only):

```
publish_ugc.py        (committed)
init_test.py          (committed)
published_id.txt      (committed — public id, not a secret)
README.md             (committed)
steamworks/        ★  (vendored from philippj/SteamworksPy)
SteamworksPy64.dll ★  (vendored shim, SDK 1.64)
steam_api64.dll    ★  (SDK >=1.57 with SteamInternal_SteamAPI_Init)
steam_appid.txt    ★  (839770)
```

## Run

Steam must be running and logged in as the item **owner**. Run from this folder.

```powershell
# Smoke test — confirms bind to appid 839770 + logged-in user:
python init_test.py

# First publish (creates a brand-new public item, then uploads):
python publish_ugc.py --create --changenote "v1.0.0 initial release" --visibility public

# Future updates (re-upload to the existing item):
python publish_ugc.py --update --item 3739613434 --changenote "v1.1.0"
```

Options: `--visibility public|friends|private`, `--tags a,b,c` (default none),
`--changenote "..."`.

On success the script writes `published_id.txt` and stamps the id into
`../oracle.vdf` (the SteamCMD fallback descriptor).

## Notes

- If `CreateItem` reports the **workshop legal agreement** flag, accept it once
  in the browser/Steam client at the item URL; the script prints a clear notice.
- Tags are left empty by default — Phoenix Point's valid Workshop tags are not
  validated here, and an unknown tag can fail the submit.
- Gallery screenshots (`image/screenshot1.jpg`, `screenshot2.jpg`) are **not**
  set by the UGC API; add them via the item's web page if desired.
