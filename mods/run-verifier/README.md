# Run Verifier 1.3.0

[Technical documentation](docs/index.md).

Offline completion history, native results-screen seals, area timings, fall and
recovery statistics, and profile-linked certificates at https://autocubes.site/jumpking.
Requires **JK Runtime 1.30+**. **Replays 2.2+** is optional.
All interface text is English. This mod does not change movement or register a
modifying body behavior.

## Playing

Open **Mods > Run Verifier > Run history** in the main or pause menu. History works offline by default.
Open **Settings > Sign in to website** to create an independent profile and open
a one-time sign-in code. No Steam login is required. Steam ID is self-reported,
not verified account ownership. Then enable **Online + auto upload (unlisted)** before starting a
new attempt. This explicitly enables server-timed checkpoints and automatic
publication after completion, including associated replay files. Results are
unlisted: anyone with the result ID/seal can read them, but they do not appear in
public browsing until you choose **List publicly** on the website.

The upper-left corner of the results screen shows the encoded circular border and a personal
signature derived from Steam ID. The signature stays consistent across that
player's runs. Read the evidence level in history or on the website: offline
uploads are **Published**, incomplete evidence is **Partial observation**, and
fully covered online runs are **Online observed**.
Settings also offers **Upload map completion marker** for built-in maps and
installed Workshop maps, and **Upload current attempt (unverified)**. Marker
imports contain no invented time or completion date. Both are unlisted and await
moderation. The website accepts screenshot submissions and uses profile nicknames.

The Actions tab can upload/retry, open the website, export a report or favorite it.
The root menu supports search, sorting and favorites; Left/Right switches detail
tabs. Areas use player position, not the camera. Non-playing wall time includes
pause menus, focus loss and scheduling delays; it is not a precise pause counter.

Replays records one session. Save unfinished sessions manually in Replays before
leaving if you want their segments. Completed recordings attach automatically,
even when the writer finishes after the result was uploaded. Each attachment
shows its own interval; missing sessions remain missing. Playback creates no
completion. Upload limits are 64 MB per replay and 1 GB per profile.

## Verification limits

Certificates authenticate the server's accepted report, account and evidence
level. They do not prove the absence of cheating in an untrusted game client.
A copied seal does not authenticate unrelated screenshot pixels. The checker
compares the encoded fields and signed report, and lets you compare visible
screenshot fields separately. Registration, installed mods and actual mechanic
usage are distinct. Zero known flags does not mean proven vanilla play.

The round double outline repeats the payload in three sectors. Upright PNG and
JPEG screenshots support a 15% crop from one edge at 1x-4x integer scale, while
the signature initial stays visible. Severe compression, scaling or rotation can
still prevent decoding. If decoding fails,
enter the result ID. There is no OCR or arbitrary-perspective photo support.

## Storage and recovery

Files live in `Content/RunVerifier`. Back up that directory with game saves.
`.run.json` files are atomic checkpoints, `.bak` files are their previous versions,
and `.journal` files contain diagnostic checkpoint hashes. Corrupt originals are
preserved. Checkpoints are saved every 15 seconds and at session boundaries.
A missing interval, changed environment or interrupted session marks partial
coverage. Rollbacks and changed map revisions cannot merge into a newer record.
Never share `profile.json`: it contains a Windows-user-protected device token.
Keep this file and your Windows account to retain profile access; a Steam ID
cannot recover it. Browser codes expire after five minutes and work once.
Uploads are retained as `.upload.json` jobs until successful; use **Retry pending
uploads** after reconnecting. No network operation blocks gameplay.

Build: `scripts/check-mods.ps1 -Mod run-verifier,replays -Integration`.
Install: `mods/run-verifier/install.ps1`, with Jump King closed.
See the [protocol](docs/protocol.md) and [release checks](docs/validation.md).

Public distribution is Steam Workshop only. The item is not published yet.
