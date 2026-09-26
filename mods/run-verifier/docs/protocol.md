# Run Verifier protocol 1

The website serves `/api/jumpking/v1`. Production origin is
`https://autocubes.site`; only HTTPS is used by the mod. All JSON is UTF-8.

## Identity and publication

`POST profile/register` accepts a client-generated 256-bit bearer secret saved
with Windows DPAPI before the first request. Registration is idempotent for that
credential and reported Steam ID; the server assigns a random profile ID.
The server obtains the nickname from Steam Community using the reported Steam ID,
caches it for six hours and refreshes it on sign-in. There is no manual nickname
endpoint. Submitted client names do not override Steam metadata. Submission names
are snapshots; changing a Steam nickname does not rewrite earlier certificates.
Steam ID is unverified metadata and never grants ownership or recovery access.
Device credentials last ten years; the server stores only their SHA-256 hashes.

`POST auth/code/start` requires a device credential and creates a 48-bit display
code valid for five minutes, invalidating that device's previous code. Codes are
hashed at rest. `POST auth/code/exchange` requires the configured Origin even
without an existing session, consumes the code once and creates a 30-day
HttpOnly/Secure/SameSite=Lax browser cookie. Revoked/expired devices cannot issue
usable codes. Registration, code creation and redemption are rate-limited.
Codes travel in the URL fragment, which the browser removes immediately.

`POST runs` accepts a completed schema-1 report bound to the profile and its reported Steam
ID. It whitelists report properties and validates counters, native timer fields,
coverage, session intervals and bounded arrays. The server calculates SHA-256 of
canonical report JSON: recursively sorted object keys, preserved array order,
ordinary JSON numbers and UTF-8 encoding. An ID already containing different
report bytes is rejected. Retrying identical content returns the original receipt.

Receipts use RSA-2048 / RSASSA-PKCS1-v1_5 / SHA-256 (RS256), supported by the
game's .NET Framework without a bundled cryptographic library. The signed payload
contains profile ID, owner ID, playerName, steamVerified=false, schema, ID, Steam ID, map ID/revision, time, native modifier peak, report
digest, evidence level, issued UTC and `hybrid-v1` policy. `payload` and `signature`
are base64. `keyId` identifies the public PEM/XML key at `GET keys/{keyId}`.
The persistent private key never leaves the server. The browser verifies the
signature and canonical report digest. Revocation is a separate live server state.
Key replacement is an operator migration, not an automatic fallback for a lost key.

## Observation

`observe/start` creates a unique server challenge for run/session/revision/profile.
The mod sends a checkpoint every five wall seconds, including while paused.
Each batch contains sequence, previous hash and an exact JSON payload string
with run ID, game time and sampled Y. The next hash is SHA-256 of
`previous + "\n" + payload`. A duplicate last batch is idempotent; changed,
out-of-order or backwards-clock batches are rejected.

An interval exceeding 45 wall seconds, advancing more than 15 game seconds,
or advancing more than wall time plus 3 seconds creates a gap. Finalization
requires closed observations for every recorded session, no gaps, matching
boundaries within one second, a start below one second and total observed play
time within two seconds of native completion time. Client-reported uncertainty
prevents full observation. This is server-timed evidence, not trusted execution.
The installed game's `Game1.MyInit` sets `TimeSpan.FromSeconds(1f/60f)`.
On .NET Framework this is 17 ms. Timing checks use that native interval and
continue checking during play; an exact mathematical 1/60 comparison is incorrect.
Offline uploads are `published`; incomplete observations are `partial`; matching
complete coverage is `online-observed`.

## Seal

The seal is a 124 by 124 pixel circle. Three sectors each carry the same 592
bits: a 32-bit marker `0xd3a5c69b`, least-significant bit first, followed by
Hamming(7,4) codewords. Candidate 2 by 2 pixel cells are enumerated in
y/x order on a 62 by 62 grid, centered at (30.5, 30.5), with squared radius in
[18^2, 30^2]. Normalize atan2 to [0, 2*pi) and divide into three 120-degree sectors.
For bit i select candidate floor(i * sectorCount / 592). All cells are disjoint.
The signature's fixed initial at (35, 48) is a 9 by 9 marker with 2-pixel cells.
The exact patterns are
published in RoundSeal.Finder and jumpking-seal.ts. Try each complete sector
and then majority votes from available cells; the Hamming and CRC checks apply.
Pixels outside the image are erasures, never clamped to an unrelated edge pixel.

The signature suffix is derived from SteamID64 using the SplitMix64 finalizer
(with its standard increment), followed by eight quadratic pen strokes and an
underline. Arithmetic is unsigned 64-bit, with integer rasterization. C# and
TypeScript share a signature raster test vector. This is a stable visual identity,
not a private signing key or proof of account ownership. Result ID, time, Steam ID
and flags stay encoded in the border; there are no printed seal labels.
The signature initial must remain visible for detection after edge cropping.

The payload is 40 bytes: ID (16 bytes in displayed hex order), Steam ID (uint64
little endian), rounded elapsed milliseconds (uint64 LE), native modifier peak
(uint32 LE), then CRC-32/IEEE of the preceding 36 bytes (uint32 LE). Encode low
nibble then high nibble. Hamming positions 1/2/4 are parity; positions 3/5/6/7
are the nibble bits from least significant to most significant. The CRC rejects
uncorrectable payloads. A seal is a lookup reference; cryptographic authenticity
comes from the server receipt, not a client-side secret.

Shared test vector: ID `00112233445566778899aabbccddeeff`, Steam
`76561198000000000`, milliseconds `1234567`, flags `3` encodes as
`ABEiM0RVZneImaq7zN3u/wBMXgIBABABh9YSAAAAAAADAAAAnl2X+w==`.

## Replay attachments and storage

Replays bridge v1 associates each immutable saved recording with its capture-time
run and session. Late attachments use `POST runs/{id}/attachments`, then raw
gzip `.jkr` bytes at `POST runs/{id}/replay/{attachmentId}`. The server checks
ownership, SHA-256, size, format v2, frame structure, map identity/revision and
declared interval. Each attachment receives a separate signed statement; adding
one never rewrites the completion certificate. This positional recording does
not deterministically replay game physics. Maximum upload: 64 MiB; account replay
quota: 1 GiB; report quota: 10,000 completions; 100 attachments per result.

Server data uses transactional SQLite WAL with synchronous FULL on a persistent
volume, a persistent RSA key and immutable replay files. Browser screenshot
decoding stays local and accepts at most 20 MiB / 16 megapixels, with a 45-second
worker deadline. It supports upright axis-aligned seals and offers cropping and
manual ID fallback. Signature validity and visible-field consistency are separate.


## Unverified submissions and moderation

`POST images` accepts base64 still PNG/JPEG/WebP up to 8 MiB and 16 megapixels.
The server strips metadata, normalizes to PNG and binds the image to the profile.
`POST submissions` accepts screenshot, marker or attempt sources, a completion
boolean, nullable time/date, map identity/name, note and requested visibility.
Marker and attempt sources require a device credential. Markers cannot claim
elapsed time. All such submissions have no certificate and await moderation.
Idempotent IDs and image/content deduplication prevent accidental duplicate posts.

`GET maps`, `GET maps/{id}` and `GET profiles/{id}` expose the public catalog,
canonical map names and personal bests grouped by revision/category/evidence.
Owner and moderator roles are independent of Steam identity. Administration can
review results, restrict publishing, resolve reports and merge duplicate maps.
Only the configured owner may appoint moderators. Actions are audited. Accepting
an unverified submission never grants recorded or online-observed evidence.
