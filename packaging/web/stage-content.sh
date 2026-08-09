#!/bin/sh
# Packages the mod files and the Red Alert freeware content for the browser build.
#
# The engine reads over a thousand small files out of mods/. Fetching those
# individually would mean a request each, so they are shipped as archives and
# unpacked into the virtual filesystem in one pass. This is also the shape the
# on-chain delivery needs later: one addressable blob per mod.
#
# Usage: packaging/web/stage-content.sh

set -eu

ENGINE_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")/../.." && pwd)"
OUTPUT_DIR="$ENGINE_DIR/OpenRA.Web/wwwroot/content"
STAGING="$ENGINE_DIR/OpenRA.Platforms.Web/native/staging"

mkdir -p "$OUTPUT_DIR" "$STAGING"

# --- engine files ----------------------------------------------------------
# Only the mods needed to reach Red Alert: the mod itself, the shared rules it
# builds on, and the content-installer mods the manifest references.
if [ ! -f "$OUTPUT_DIR/engine.zip" ]; then
	echo "Packaging engine files..."
	rm -rf "$STAGING/engine"
	mkdir -p "$STAGING/engine"
	for dir in mods/ra mods/common mods/all mods/ra-content mods/common-content glsl; do
		mkdir -p "$STAGING/engine/$(dirname "$dir")"
		cp -r "$ENGINE_DIR/$dir" "$STAGING/engine/$dir"
	done

	# Filenames inside .mix packages are stored as hashes; this maps them back to
	# names. Without it, lookups by name inside those packages fail.
	cp "$ENGINE_DIR/global mix database.dat" "$STAGING/engine/"
	cp "$ENGINE_DIR/VERSION" "$STAGING/engine/" 2>/dev/null || true

	(cd "$STAGING/engine" && zip -qr "$OUTPUT_DIR/engine.zip" .)
	echo "  $(du -h "$OUTPUT_DIR/engine.zip" | cut -f1)"
fi

# --- Red Alert freeware content -------------------------------------------
# EA released Red Alert as freeware; this is the package OpenRA's own installer
# downloads. The SHA1 is the one recorded in mods/ra-content/installer/downloads.yaml.
CONTENT_SHA1="44241f68e69db9511db82cf83c174737ccda300b"

if [ ! -f "$OUTPUT_DIR/ra-content.zip" ]; then
	echo "Fetching Red Alert freeware content..."
	MIRROR="$(curl -fsSL https://www.openra.net/packages/ra-quickinstall-mirrors.txt | head -1 | tr -d '\r')"
	curl -fsSL "$MIRROR" -o "$OUTPUT_DIR/ra-content.zip.tmp"

	ACTUAL="$(sha1sum "$OUTPUT_DIR/ra-content.zip.tmp" | cut -d' ' -f1)"
	if [ "$ACTUAL" != "$CONTENT_SHA1" ]; then
		rm -f "$OUTPUT_DIR/ra-content.zip.tmp"
		echo "error: content checksum mismatch (expected $CONTENT_SHA1, got $ACTUAL)" >&2
		exit 1
	fi

	mv "$OUTPUT_DIR/ra-content.zip.tmp" "$OUTPUT_DIR/ra-content.zip"
	echo "  $(du -h "$OUTPUT_DIR/ra-content.zip" | cut -f1), sha1 verified"
fi

echo "Staged in $OUTPUT_DIR"
