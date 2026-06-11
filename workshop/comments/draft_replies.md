# Drafting Workshop comment replies

A lightweight workflow for answering Oracle Workshop comments in the mod's
voice, in English and Russian.

## Workflow

1. **Read** new comments (no login needed):

   ```powershell
   pip install -r workshop/comments/requirements.txt   # once
   python workshop/comments/read_comments.py --owner <YourSteamID64> --item <publishedfileid> --count 50
   ```

   (Find your SteamID64 via steamid.io / steamdb; find the publishedfileid in the
   Workshop item URL `?id=...`.)

2. **Paste** a comment to your assistant like:

   > Draft EN+RU replies to this Workshop comment: "<paste comment text>"

3. You get back a short EN reply and a short RU reply, ready to paste.

4. **Post** the reply **manually in the browser** (recommended). The
   `post_comment.py` writer is experimental, unofficial, and risky — see the big
   warning at the top of that file.

## Tone guidelines

- **Helpful and concise.** Answer the actual question; skip filler.
- **Thank reporters.** A quick "thanks for the report / for trying the mod" goes a
  long way.
- **Bug reports:** ask for the specifics needed to reproduce —
  - a **save file** at/just before the issue,
  - the **`Player.log`** (Phoenix Point output log),
  - the **mod load order** (confirm TFTV is installed and Oracle loads after it),
  - exact **steps** and what they expected vs. saw.
- **Set expectations honestly.** If something is by design (e.g. fixed/class cells
  are never highlighted, or `AllowPerkSwap` is off by default), say so kindly.
- **Point to the source** for power users: the GitHub repo issues page is the best
  place for detailed bug reports.
- **Stay positive and neutral.** No arguing; assume good faith.

## Reusable snippets

- EN bug-ask: "Thanks for the report! Could you share your `Player.log` and a save
  from just before it happens, plus confirm TFTV is installed and Oracle loads
  after it? That'll help me reproduce it."
- RU bug-ask: «Спасибо за репорт! Можете приложить `Player.log` и сейв прямо перед
  проблемой, а также подтвердить, что TFTV установлен и Oracle грузится после
  него? Так я смогу воспроизвести.»
- EN thanks: "Glad it's useful — thanks for trying Oracle!"
- RU thanks: «Рад, что пригодилось — спасибо, что попробовали Oracle!»
