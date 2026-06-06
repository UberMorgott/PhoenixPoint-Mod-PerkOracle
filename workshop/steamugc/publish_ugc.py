"""
Headless Steam Workshop publisher for the PerkOracle Phoenix Point mod.

Rides the ALREADY-RUNNING, logged-in Steam client via SteamworksPy (no
username/password) -- the same auth model the official PPWorkshopTool uses.

Requirements (all live next to this script, in workshop/steamugc/):
  * steamworks/                 (the SteamworksPy python package)
  * SteamworksPy64.dll          (native ctypes shim, Steamworks SDK 1.64)
  * steam_api64.dll             (must export SteamInternal_SteamAPI_Init,
                                 i.e. SDK >= ~1.57; see README.md)
  * steam_appid.txt             (contains exactly: 839770)
Steam must be running and logged in as the account that will OWN the item.

Usage:
  # First publish (creates a brand-new public Workshop item):
  python publish_ugc.py --create --changenote "v1.0.0 initial release" --visibility public

  # Subsequent updates (re-upload content to an existing item):
  python publish_ugc.py --update --item 1234567890 --changenote "v1.1.0"

On success: prints the publishedfileid + item URL, writes published_id.txt,
and stamps the id into ../perkoracle.vdf (SteamCMD fallback path).
"""
import argparse
import os
import sys
import time

# --- Resolve paths BEFORE any chdir (content/preview must be absolute) --------
HERE = os.path.dirname(os.path.abspath(__file__))            # workshop/steamugc
WORKSHOP_DIR = os.path.dirname(HERE)                          # workshop
REPO_ROOT = os.path.dirname(WORKSHOP_DIR)                     # repo root

CONTENT_FOLDER = os.path.join(WORKSHOP_DIR, "Dist")
PREVIEW_FILE = os.path.join(REPO_ROOT, "image", "steam_preview.jpg")
DESC_EN = os.path.join(WORKSHOP_DIR, "description_en.txt")
DESC_RU = os.path.join(WORKSHOP_DIR, "description_ru.txt")
VDF_FILE = os.path.join(WORKSHOP_DIR, "perkoracle.vdf")
PUBLISHED_ID_FILE = os.path.join(HERE, "published_id.txt")

APP_ID = 839770
TITLE = "PerkOracle"

# Native libs + steam_appid.txt resolve relative to CWD inside SteamworksPy,
# so run from this directory.
os.chdir(HERE)
sys.path.insert(0, HERE)

from steamworks import STEAMWORKS                                      # noqa: E402
from steamworks.enums import (                                        # noqa: E402
    EWorkshopFileType,
    ERemoteStoragePublishedFileVisibility,
)

ERESULT_OK = 1  # steamworks.enums.EResult.OK

VISIBILITY_MAP = {
    "public": ERemoteStoragePublishedFileVisibility.PUBLIC,
    "friends": ERemoteStoragePublishedFileVisibility.FRIENDS_ONLY,
    "private": ERemoteStoragePublishedFileVisibility.PRIVATE,
}


def build_description() -> str:
    """English description first, then a Russian section (single Steam field)."""
    with open(DESC_EN, "r", encoding="utf-8") as f:
        en = f.read().strip()
    with open(DESC_RU, "r", encoding="utf-8") as f:
        ru = f.read().strip()
    separator = "\n\n[hr][/hr]\n\n[h1]Русский / Russian[/h1]\n\n"
    combined = en + separator + ru
    if len(combined) > 8000:
        raise SystemExit(
            f"Combined description is {len(combined)} chars, exceeds Steam's 8000 limit."
        )
    return combined


def pump_until(steam, holder: dict, timeout: float, label: str) -> dict:
    """Pump RunCallbacks until `holder` is populated by the async callback."""
    deadline = time.time() + timeout
    while not holder:
        steam.run_callbacks()
        if time.time() > deadline:
            raise SystemExit(f"TIMEOUT waiting for {label} after {timeout:.0f}s")
        time.sleep(0.1)
    return holder


def create_item(steam) -> dict:
    """CreateItem (async). Returns dict with id + legal-agreement flag."""
    holder: dict = {}

    def on_created(result):
        holder["result"] = int(result.result)
        holder["id"] = int(result.publishedFileId)
        holder["needs_legal"] = bool(result.userNeedsToAcceptWorkshopLegalAgreement)

    print(f"[create] CreateItem(app={APP_ID}, COMMUNITY) ...")
    steam.Workshop.CreateItem(
        APP_ID, EWorkshopFileType.COMMUNITY,
        callback=on_created, override_callback=True,
    )
    pump_until(steam, holder, timeout=60.0, label="CreateItemResult_t")

    if holder["result"] != ERESULT_OK:
        raise SystemExit(
            f"CreateItem FAILED: EResult={holder['result']} "
            f"(see steamworks.enums.EResult). No item was created."
        )
    print(f"[create] OK -> publishedfileid={holder['id']}")
    return holder


def submit_update(steam, published_file_id: int, description: str,
                  visibility, changenote: str, tags: list) -> dict:
    """StartItemUpdate -> set fields -> SubmitItemUpdate (async, uploads)."""
    handle = steam.Workshop.StartItemUpdate(APP_ID, published_file_id)
    print(f"[update] StartItemUpdate handle={handle}")

    steam.Workshop.SetItemTitle(handle, TITLE)
    steam.Workshop.SetItemDescription(handle, description)
    steam.Workshop.SetItemContent(handle, CONTENT_FOLDER)
    steam.Workshop.SetItemPreview(handle, PREVIEW_FILE)
    steam.Workshop.SetItemVisibility(handle, visibility)
    if tags:
        steam.Workshop.SetItemTags(handle, tags)
    print(f"[update] title/desc/content/preview/visibility set "
          f"(content={CONTENT_FOLDER}, preview={PREVIEW_FILE}, "
          f"visibility={visibility.name}, tags={tags or 'none'})")

    holder: dict = {}

    def on_submitted(result):
        holder["result"] = int(result.result)
        holder["id"] = int(result.publishedFileId)
        holder["needs_legal"] = bool(result.userNeedsToAcceptWorkshopLegalAgreement)

    print(f"[update] SubmitItemUpdate(changenote={changenote!r}) ... (uploading)")
    steam.Workshop.SubmitItemUpdate(
        handle, changenote,
        callback=on_submitted, override_callback=True,
    )

    # Pump + log upload progress until the submit callback fires.
    deadline = time.time() + 900.0  # 15 min for upload
    last_pct = -1
    while not holder:
        steam.run_callbacks()
        try:
            prog = steam.Workshop.GetItemUpdateProgress(handle)
            pct = int(prog["progress"] * 100)
            if pct != last_pct and prog["total"]:
                print(f"[update] {prog['status'].name} "
                      f"{prog['processed']}/{prog['total']} ({pct}%)")
                last_pct = pct
        except Exception:
            pass
        if time.time() > deadline:
            raise SystemExit("TIMEOUT waiting for SubmitItemUpdateResult_t after 900s")
        time.sleep(0.25)

    if holder["result"] != ERESULT_OK:
        raise SystemExit(
            f"SubmitItemUpdate FAILED: EResult={holder['result']} "
            f"(see steamworks.enums.EResult)."
        )
    print(f"[update] OK -> upload committed for id={holder['id']}")
    return holder


def persist_id(published_file_id: int) -> None:
    with open(PUBLISHED_ID_FILE, "w", encoding="utf-8") as f:
        f.write(str(published_file_id))
    print(f"[persist] wrote {PUBLISHED_ID_FILE}")

    # Stamp the id into the SteamCMD .vdf (replace placeholder or prior id).
    try:
        with open(VDF_FILE, "r", encoding="utf-8") as f:
            vdf = f.read()
        import re
        new_vdf = re.sub(
            r'("publishedfileid"\s*")[^"]*(")',
            lambda m: f'{m.group(1)}{published_file_id}{m.group(2)}',
            vdf,
        )
        if new_vdf != vdf:
            with open(VDF_FILE, "w", encoding="utf-8") as f:
                f.write(new_vdf)
            print(f"[persist] stamped publishedfileid={published_file_id} into {VDF_FILE}")
        else:
            print(f"[persist] WARNING: no publishedfileid line replaced in {VDF_FILE}")
    except FileNotFoundError:
        print(f"[persist] WARNING: {VDF_FILE} not found; skipped vdf stamp")


def main() -> int:
    ap = argparse.ArgumentParser(description="Headless SteamworksPy Workshop publisher for PerkOracle")
    mode = ap.add_mutually_exclusive_group(required=True)
    mode.add_argument("--create", action="store_true",
                      help="Create a brand-new Workshop item, then upload content.")
    mode.add_argument("--update", action="store_true",
                      help="Update an existing item (requires --item).")
    ap.add_argument("--item", type=int, default=0,
                    help="Existing publishedfileid (required with --update).")
    ap.add_argument("--changenote", default="v1.0.0 initial release",
                    help="Change note shown in the item's history.")
    ap.add_argument("--visibility", choices=list(VISIBILITY_MAP), default="public",
                    help="Item visibility (default: public).")
    ap.add_argument("--tags", default="",
                    help="Comma-separated Workshop tags (default: none).")
    args = ap.parse_args()

    if args.update and not args.item:
        ap.error("--update requires --item <publishedfileid>")

    # Sanity-check inputs.
    for p, what in [(CONTENT_FOLDER, "content folder"), (PREVIEW_FILE, "preview"),
                    (DESC_EN, "EN description"), (DESC_RU, "RU description")]:
        if not os.path.exists(p):
            raise SystemExit(f"Missing {what}: {p}")

    visibility = VISIBILITY_MAP[args.visibility]
    tags = [t.strip() for t in args.tags.split(",") if t.strip()]
    description = build_description()

    print("=" * 70)
    print("PerkOracle Workshop publisher (SteamworksPy, headless)")
    print(f"  mode        : {'create' if args.create else 'update'}")
    print(f"  app_id      : {APP_ID}")
    print(f"  content     : {CONTENT_FOLDER}")
    print(f"  preview     : {PREVIEW_FILE}")
    print(f"  visibility  : {args.visibility}")
    print(f"  changenote  : {args.changenote}")
    print(f"  desc length : {len(description)} chars")
    print("=" * 70)

    steam = STEAMWORKS()
    steam.initialize()
    print(f"[init] SteamworksPy ready: appid={steam.app_id}, "
          f"SteamID={steam.Users.GetSteamID()}, user={steam.Friends.GetPlayerName().decode(errors='replace')}")
    if steam.app_id != APP_ID:
        raise SystemExit(f"Bound to wrong appid {steam.app_id}, expected {APP_ID}")

    needs_legal = False
    if args.create:
        created = create_item(steam)
        published_file_id = created["id"]
        needs_legal = needs_legal or created["needs_legal"]
    else:
        published_file_id = args.item
        print(f"[update] using existing publishedfileid={published_file_id}")

    submitted = submit_update(steam, published_file_id, description,
                              visibility, args.changenote, tags)
    needs_legal = needs_legal or submitted["needs_legal"]
    published_file_id = submitted["id"] or published_file_id

    persist_id(published_file_id)

    url = f"https://steamcommunity.com/sharedfiles/filedetails/?id={published_file_id}"
    print("=" * 70)
    print("PUBLISH RESULT")
    print(f"  publishedfileid : {published_file_id}")
    print(f"  item URL        : {url}")
    print(f"  upload          : committed (SubmitItemUpdate returned EResult.OK)")
    if needs_legal:
        print("  ACTION REQUIRED : Steam reports you must ACCEPT THE WORKSHOP LEGAL")
        print("                    AGREEMENT once. Open the item URL above in a browser")
        print("                    (or the Steam client) and accept the agreement, or the")
        print("                    item may stay hidden. This is a one-time per-account step.")
    else:
        print("  legal agreement : not flagged")
    print("=" * 70)

    steam.unload()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
