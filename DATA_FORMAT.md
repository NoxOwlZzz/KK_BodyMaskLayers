# Persistence and data format

## Storage location

The plugin GUID is `com.nightowlzzz.koikatsu.bodymasklayers`. Extended Save attaches a `PluginData` record to each `ChaFileClothes` object. The binary payload is stored at key `Payload`.

KK and KKS builds use this same GUID, key, public namespace, and schema. Their assembly names differ, but the payload contains no assembly-qualified type names. KKS support does not add or renumber serialized slots.

Because the outfit object owns the record:

- each of the character's coordinates can hold different layers;
- a character card transports all outfit records saved by the game;
- a coordinate file transports the current outfit record;
- original PNG bytes travel inside that Extended Save data;
- no source path is required after import.

This plugin does not write into KCOX, ChaAlphaMask, Material Editor or another plugin's `PluginData`.

The equipped clothing identity selects compatible metadata from Sideloader's session manifests, then the built-in provider loads the declared source texture. A successfully resolved source is encoded automatically as a schema 2 layer with its binding, source contract, provenance, and fingerprint. This does not modify the zipmod or overwrite an existing native layer; a normal card or coordinate save persists the imported copy.

## Binary schemas BML1 and BML2

All multibyte integers use the .NET `BinaryWriter` little-endian representation. Booleans use one byte. Strings are length-prefixed strict UTF-8 without a terminator. The four-byte magic remains `BML1` so existing Extended Save payload detection does not change; the following integer is the authoritative schema version.

Payload header:

| Field | Type | Constraint |
|---|---|---|
| Magic | 4 bytes | ASCII `BML1` |
| Schema version | Int32 | `1` (read compatibility) or `2` (current write) |
| Layer count | Int32 | `0..9` |
| Layers | repeated | exactly `Layer count` records |

### Schema 1 record

Schema 1 records are unframed and retain their original order:

| Field | Type | Notes |
|---|---|---|
| Slot | Int32 | `0..8` |
| Enabled | Boolean | User contribution toggle |
| Width | Int32 | Decoded/imported width |
| Height | Int32 | Decoded/imported height |
| Color format version | Int32 | Runtime support requires `1` |
| Has optional state policy | Boolean | Controls next field |
| Optional state policy | Int32 | Present only when preceding value is true |
| SHA-256 | String | Lowercase diagnostic/content hash |
| Created-with plugin version | String | Writer version metadata |
| Last validation result | String | Diagnostic metadata |
| Has bound identity | Boolean | Controls identity block |
| Bound identity | block | Present only when preceding value is true |
| PNG length | Int32 | Positive length up to the 32 MiB cap |
| Original PNG | bytes | Exactly PNG length bytes |

When schema 1 is loaded, the deterministic migration defaults are:

- source contract: `Native`;
- gradient handling: `StrictCategorical`, giving schema 1 a deterministic palette interpretation;
- source provider ID, fingerprint and asset: null.

The next save writes schema 2. Migration keeps the original PNG byte-for-byte and does not serialize a compiled mask.

### Schema 2 current record

Each schema 2 layer starts with an `Int32` record length. The length covers the fields after the prefix and is bounded by the metadata envelope plus the 32 MiB image cap. A record must be consumed exactly, which isolates truncation or malformed variable-length fields to that payload failure.

The framed record contains the schema 1 fields through the optional bound identity, followed by:

| Field | Type | Notes |
|---|---|---|
| Source contract | Int32 enum | `Native = 0`, `ExternalRgbStateCoverage = 1` |
| Gradient handling | Int32 enum | `Auto = 0`, `PreserveContinuous = 1`, `StrictCategorical = 2` |
| Source provider ID | String | Optional provenance; maximum 256 UTF-8 bytes |
| Source fingerprint | String | Optional stable content/metadata fingerprint; maximum 512 UTF-8 bytes |
| Source asset | String | Optional source locator for diagnostics/conversion |
| PNG length | Int32 | Positive length up to the 32 MiB cap |
| Original PNG | bytes | Exactly PNG length bytes |

The source fields describe how to rebuild the runtime representation. `ExternalRgbStateCoverage` means stored PNG R/G/B are direct hide-coverage planes for states 0/1/2; state 3 is neutral. Runtime slot visibility is applied before selecting a plane without changing the stored channel meanings. These fields do not embed continuous coverage planes, bitsets, provider caches or other compiled buffers.

Bound identity block (unchanged in both schemas):

| Field | Type | Notes |
|---|---|---|
| Identity slot | Int32 | Must be in `0..8` |
| Category | Int32 | Game list category |
| Local item ID | Int32 | Runtime/local fallback |
| Original item ID | Int32 | Sideloader stable original slot when resolved |
| Sideloader GUID | String | Null when unresolved/vanilla |
| Display name | String | Diagnostic/UI label, not match authority |

Binding policy is not serialized as a mode. Every layer always requires both the owning slot and the stored item identity to match the currently equipped garment. The Maker action **Bind mask to current item** deliberately replaces the stored identity when a compatible garment is substituted.

String encoding:

- `-1` length means null;
- `0` means an empty string;
- the general maximum encoded length is 4096 bytes;
- provider IDs and fingerprints use the narrower limits above;
- invalid UTF-8 is rejected.

There may be no trailing bytes after a schema 2 record or after the final layer. Duplicate slot records are accepted by the low-level reader with the last record winning; normal writers provide at most one record per slot.

## Runtime-only representation

After validation the PNG is decoded into a `SemanticMask`. Depending on its source contract and interpretation mode, the runtime mask can use the compact binary representation or continuous per-state coverage. The decoded mask, resampled planes and composed output are runtime caches; they are not serialized. The original PNG remains the portable source of truth.

## Validation and corruption handling

The reader rejects:

- missing/truncated data or invalid magic;
- schema versions other than 1 or 2;
- negative or greater-than-nine layer counts;
- slots outside 0..8;
- invalid schema 2 record lengths, resolutions, enum values or non-positive color-format versions;
- invalid string lengths/UTF-8;
- missing, oversized or truncated PNG data;
- unexpected trailing bytes within a schema 2 record or after the payload.

Deserialization is atomic at the outfit level. On failure, partially read records are discarded and the character continues without applying that payload. The diagnostic warning should be preserved with the failing card/coordinate for reproduction.

After envelope deserialization, each layer is activated only when `Color format version == 1`, the recomputed PNG SHA-256 matches any stored hash, and PNG/header/color validation succeeds. The envelope accepts positive future color-format values so their bytes remain transportable, but runtime leaves any value other than `1` inactive. A future or corrupt color format or hash mismatch therefore emits a warning instead of being interpreted as current data.

## Format safeguards

The binary format and import pipeline use a fixed 32 MiB PNG cap to prevent hostile allocations. Image dimensions must also be square, power-of-two, positive, and safely representable by the runtime. These safeguards are internal rather than user settings.

## Card and coordinate behavior

Full card/coordinate loads follow KKAPI/Extended Save callbacks. Maker load flags preserve current clothes data when Clothes is not selected.

Both APIs resolve coordinate index `-1` to the transient `nowCoordinate` and a nonnegative index to `chaFile.coordinate[index]`. Writes update both the active transient outfit and its valid indexed counterpart. KK normally has seven coordinate types and KKS four; the controller checks the actual array length instead of assuming a fixed count.

The serialized slot map remains `Top=0`, `Bottom=1`, `Bra=2`, `Shorts=3`, `Gloves=4`, `Pantyhose=5`, `Socks=6`, `IndoorShoes=7`, and `OutdoorShoes=8`. KKS still has nine clothing-data slots and uses slot `8` for its Maker shoes tab. Omitting the Indoor Shoes controls does not remove slot `7` from saved data or renumber slot `8`.

The shared mask format does not imply verified cross-game card compatibility. Clothing categories and identities must still resolve in the destination game, and the games' coordinate layouts differ. No cross-game card or coordinate migration is performed.

KK Coordinate Load Option `21.1.4` bypasses unknown plugin payloads during partial per-slot copies. The optional adapter merges schema records at slot granularity: selected slots are replaced or cleared according to the source; unselected slots are retained byte-for-byte at the logical record level. If its version/signature check fails, the adapter makes no metadata change.

The KKS partial-slot adapter is disabled. KKS Coordinate Load Option `21.12.23.0` can abort its clothing transfer without returning a success indicator, so an unconditional post-transfer merge could replace masks even when their garments were not copied. Use complete coordinate loading for mask data in KKS; the normal KKAPI/Extended Save path is independent of this optional bridge.

## Forward compatibility

Readers currently reject unknown schema versions instead of guessing. Any future schema must:

1. increment the version;
2. document a deterministic migration from schema 2;
3. preserve original PNG bytes and item identity;
4. keep supported card and coordinate loading non-destructive;
5. add pure-logic corruption and round-trip tests before release.

`Color classification`, `ColorTolerance`, and `UnknownColorPolicy` affect decoding of existing native masks without rewriting their PNG bytes. `Gradient Handling` is stored per layer; changing its global default affects later imports, not the interpretation stored in an existing layer. External RGB coverage is decoded according to its source contract.
