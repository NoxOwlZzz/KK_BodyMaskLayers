# Persistence and data format

## Storage location

The plugin GUID is `com.nightowlzzz.koikatsu.bodymasklayers`. Extended Save attaches a `PluginData` record to each `ChaFileClothes` object. The binary payload is stored at key `Payload`.

Because the outfit object owns the record:

- each of the character's coordinates can hold different layers;
- a character card transports all outfit records saved by the game;
- a coordinate file transports the current outfit record;
- original PNG bytes travel inside that Extended Save data;
- no source path is required after import.

This plugin does not write into KCOX, ChaAlphaMask, Material Editor or another plugin's `PluginData`.

## Binary schema BML1

All multibyte integers use the .NET `BinaryWriter` little-endian representation. Booleans use its one-byte representation. Strings are length-prefixed strict UTF-8 without a terminator.

Payload header:

| Field | Type | Constraint |
|---|---|---|
| Magic | 4 bytes | ASCII `BML1` |
| Schema version | Int32 | `1` |
| Layer count | Int32 | `0..9` |
| Layers | repeated | exactly `Layer count` records |

Layer record, in order:

| Field | Type | Notes |
|---|---|---|
| Slot | Int32 | `0..8` |
| Enabled | Boolean | User contribution toggle |
| Width | Int32 | Decoded/imported width |
| Height | Int32 | Decoded/imported height |
| Color format version | Int32 | `1` in the current format |
| Has optional state policy | Boolean | Controls next field |
| Optional state policy | Int32 | Present only when preceding value is true |
| SHA-256 | String | Lowercase diagnostic/content hash |
| Created-with plugin version | String | Writer version metadata |
| Last validation result | String | Diagnostic metadata |
| Has bound identity | Boolean | Controls identity block |
| Bound identity | block | Present only when preceding value is true |
| PNG length | Int32 | `1..33554432` absolute format cap |
| Original PNG | bytes | Exactly PNG length bytes |

Bound identity block:

| Field | Type | Notes |
|---|---|---|
| Identity slot | Int32 | Must correspond to the intended slot |
| Category | Int32 | Game list category |
| Local item ID | Int32 | Runtime/local fallback |
| Original item ID | Int32 | Sideloader stable original slot when resolved |
| Sideloader GUID | String | Null when unresolved/vanilla |
| Display name | String | Diagnostic/UI label, not match authority |

String encoding:

- `-1` length means null;
- `0` means an empty string;
- maximum encoded length is 4096 bytes;
- invalid UTF-8 is rejected.

There may be no trailing bytes after the final layer. Duplicate slot records are accepted by the low-level reader with the last record winning, but writers emit at most one record per slot.

## Runtime-only representation

After validation the PNG is decoded into a `SemanticMask` whose pixels are categorical values: `NeverHide`, `HideWhenFull`, `HideWhenNotOff` or an unknown-policy result. This decoded buffer and composed output are runtime caches; they are not serialized. The original PNG remains the portable source of truth.

## Validation and corruption handling

The reader rejects:

- missing/truncated data or invalid magic;
- schema versions other than 1;
- negative or greater-than-nine layer counts;
- slots outside 0..8;
- invalid string lengths/UTF-8;
- missing, oversized or truncated PNG data;
- unexpected trailing bytes.

Deserialization is atomic at the outfit level. On failure, partially read records are discarded and the character continues without applying that payload. The diagnostic warning should be preserved with the failing card/coordinate for reproduction.

After envelope deserialization, each layer is activated only when `Color format version == 1`, the recomputed PNG SHA-256 matches any stored hash, and PNG/header/color validation succeeds. A future or corrupt color format or hash mismatch leaves that one stored layer inactive and emits a warning instead of interpreting it as current data.

## Configuration limits versus format limits

The binary format has a hard 32 MiB PNG cap to prevent hostile allocations. The normal configurable default is 16 MiB (`MaximumPngBytes`). The plugin normally accepts 128..2048 dimensions even though configuration clamps the absolute maximum to 4096. Raising a user limit does not alter schema version 1.

## Card and coordinate behavior

Full card/coordinate loads follow KKAPI/Extended Save callbacks. Maker load flags preserve current clothes data when Clothes is not selected.

Coordinate Load Option 21.1.4 bypasses unknown plugin payloads during partial per-slot copies. The optional adapter merges schema records at slot granularity: selected slots are replaced or cleared according to the source; unselected slots are retained byte-for-byte at the logical record level. If its version/signature check fails, the adapter makes no metadata change.

## Forward compatibility

Readers currently reject unknown schema versions instead of guessing. Any future schema must:

1. increment the version;
2. document a deterministic migration from BML1;
3. preserve original PNG bytes and item identity;
4. keep old card/coordinate loading non-destructive;
5. add pure-logic corruption and round-trip tests before release.

Changing classification defaults does not rewrite the stored PNG. It only changes runtime decode behavior and can be reversed through configuration.
