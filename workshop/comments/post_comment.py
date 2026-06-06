#!/usr/bin/env python3
r"""EXPERIMENTAL Steam Workshop comment WRITER (use at your own risk).

#############################################################################
# WARNING - READ THIS BEFORE USING
#
#  * This uses an UNOFFICIAL, undocumented Steam endpoint. There is NO
#    official write API for Workshop comments.
#  * It authenticates by REUSING your logged-in browser session cookies
#    (sessionid + steamLoginSecure). That is against Steam's Web API ToS.
#  * Steam's comment flow CHANGED at Valve's 2023-10-17 update and has broken
#    third-party posters before; this may simply not work.
#  * Automating account actions this way carries a real ACCOUNT-BAN RISK.
#  * Prefer replying manually in the browser. Use this only if you fully
#    understand and accept the risk.
#
#  This script REFUSES to run unless you pass --i-understand-the-risk.
#############################################################################

Cookies are read from environment variables ONLY (never hardcoded, never
committed):

    STEAM_SESSIONID       value of the 'sessionid' cookie
    STEAM_LOGIN_SECURE    value of the 'steamLoginSecure' cookie

Get them from your browser's dev tools (Application > Cookies > steamcommunity.com)
while logged in. Do NOT paste them into any file.

Usage (PowerShell):
    $env:STEAM_SESSIONID = "..."
    $env:STEAM_LOGIN_SECURE = "..."
    python post_comment.py --owner <SteamID64> --item <publishedfileid> \
        --text "Thanks for the report!" --i-understand-the-risk
"""

import argparse
import os
import sys

import requests

POST_ENDPOINT = (
    "https://steamcommunity.com/comment/PublishedFile_Public/post/{owner}/{item}/"
)


def main() -> int:
    ap = argparse.ArgumentParser(
        description="EXPERIMENTAL: post a Steam Workshop comment via session cookies."
    )
    ap.add_argument("--owner", required=True, help="Owner SteamID64 (item author).")
    ap.add_argument("--item", required=True, help="publishedfileid of the item.")
    ap.add_argument("--text", required=True, help="Comment body.")
    ap.add_argument(
        "--i-understand-the-risk",
        dest="ack",
        action="store_true",
        help="Required acknowledgement that this is unofficial and risky.",
    )
    args = ap.parse_args()

    if not args.ack:
        print(
            "Refusing to run: pass --i-understand-the-risk to acknowledge this is\n"
            "an unofficial, ToS-violating, account-ban-risk operation.",
            file=sys.stderr,
        )
        return 2

    sessionid = os.environ.get("STEAM_SESSIONID")
    login_secure = os.environ.get("STEAM_LOGIN_SECURE")
    if not sessionid or not login_secure:
        print(
            "Missing cookies. Set STEAM_SESSIONID and STEAM_LOGIN_SECURE env vars.",
            file=sys.stderr,
        )
        return 2

    url = POST_ENDPOINT.format(owner=args.owner, item=args.item)
    cookies = {"sessionid": sessionid, "steamLoginSecure": login_secure}
    data = {"comment": args.text, "sessionid": sessionid, "count": 10}
    headers = {
        "User-Agent": "Mozilla/5.0 PerkOracle-comment-poster/0.1",
        "Referer": f"https://steamcommunity.com/sharedfiles/filedetails/?id={args.item}",
        "Origin": "https://steamcommunity.com",
    }

    try:
        resp = requests.post(url, data=data, cookies=cookies, headers=headers, timeout=30)
        resp.raise_for_status()
        payload = resp.json()
    except requests.RequestException as exc:
        print(f"Request failed: {exc}", file=sys.stderr)
        return 1
    except ValueError:
        print(f"Non-JSON response (likely auth/endpoint change):\n{resp.text[:500]}",
              file=sys.stderr)
        return 1

    if payload.get("success"):
        print("Comment posted (success=1).")
        return 0
    print(f"Steam reported failure: {payload}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
