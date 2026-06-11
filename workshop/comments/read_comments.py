#!/usr/bin/env python3
"""Read Steam Workshop item comments (READ-ONLY, no login).

Uses Steam's UNDOCUMENTED comment-render endpoint:

    POST https://steamcommunity.com/comment/PublishedFile_Public/render/{owner}/{item}/

with form fields ``start`` and ``count``. The JSON response contains
``comments_html`` (an HTML fragment) and ``total_count``. This script strips the
HTML to plain text and prints author + text + timestamp for each comment.

WARNING: this endpoint is undocumented and not part of any official Steam Web
API. Valve can change or remove it at any time without notice; if it breaks,
that is expected. It is used here for read-only convenience only.

Usage:
    python read_comments.py --owner <SteamID64> --item <publishedfileid> [--count 50]
"""

import argparse
import sys

import requests
from bs4 import BeautifulSoup

ENDPOINT = (
    "https://steamcommunity.com/comment/PublishedFile_Public/render/{owner}/{item}/"
)


def fetch_comments(owner: str, item: str, start: int, count: int) -> dict:
    url = ENDPOINT.format(owner=owner, item=item)
    resp = requests.post(
        url,
        data={"start": start, "count": count},
        headers={"User-Agent": "Oracle-comment-reader/1.0"},
        timeout=30,
    )
    resp.raise_for_status()
    return resp.json()


def parse_comments(html: str) -> list[dict]:
    soup = BeautifulSoup(html or "", "html.parser")
    out = []
    for block in soup.select(".commentthread_comment"):
        author_el = block.select_one(".commentthread_author_link")
        text_el = block.select_one(".commentthread_comment_text")
        time_el = block.select_one(".commentthread_comment_timestamp")
        author = author_el.get_text(strip=True) if author_el else "(unknown)"
        text = text_el.get_text("\n", strip=True) if text_el else ""
        # Steam puts the human-readable time in the title attribute.
        if time_el and time_el.has_attr("title"):
            ts = time_el["title"]
        elif time_el:
            ts = time_el.get_text(strip=True)
        else:
            ts = ""
        out.append({"author": author, "text": text, "time": ts})
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description="Read Steam Workshop item comments.")
    ap.add_argument("--owner", required=True, help="Owner SteamID64 (item author).")
    ap.add_argument("--item", required=True, help="publishedfileid of the item.")
    ap.add_argument("--count", type=int, default=50, help="Max comments (default 50).")
    ap.add_argument("--start", type=int, default=0, help="Offset (default 0).")
    args = ap.parse_args()

    try:
        data = fetch_comments(args.owner, args.item, args.start, args.count)
    except requests.RequestException as exc:
        print(f"Request failed: {exc}", file=sys.stderr)
        return 1

    if not data.get("success", 1):
        print(f"Steam returned success=0: {data}", file=sys.stderr)
        return 1

    total = data.get("total_count", "?")
    comments = parse_comments(data.get("comments_html", ""))
    print(f"Total comments reported: {total}; fetched: {len(comments)}\n")
    for i, c in enumerate(comments, 1):
        print(f"[{i}] {c['author']}  ({c['time']})")
        print(c["text"] or "(empty)")
        print("-" * 60)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
