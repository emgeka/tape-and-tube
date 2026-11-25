#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-3.0-only
# Native Linux core test only. Does not install system packages or alter the app.
set -euo pipefail
proto_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "$proto_root/../.." && pwd)"
cd "$project_root"
if [[ "$(uname -s)" != Linux ]]; then echo 'Run this script inside the Linux system.' >&2; exit 1; fi
missing=()
ffmpeg_tool="${TAPETUBE_FFMPEG:-ffmpeg}"
ffprobe_tool="${TAPETUBE_FFPROBE:-ffprobe}"
for tool in cargo rustc g++ cmake ninja git curl unzip sha256sum node "$ffmpeg_tool" "$ffprobe_tool"; do
    command -v "$tool" >/dev/null 2>&1 || missing+=("$tool")
done
if (( ${#missing[@]} )); then
    printf 'Missing tools: %s\n' "${missing[*]}" >&2
    echo 'See README.md in this directory. No packages have been installed.' >&2
    exit 1
fi
if ! rustc --version | node -e 'let s="";process.stdin.on("data",b=>s+=b);process.stdin.on("end",()=>{const v=s.match(/rustc (\d+)\.(\d+)\.(\d+)/);process.exit(v&&(Number(v[1])>1||Number(v[2])>=99)?0:1)})'; then
    echo 'This prototype requires Rust 1.99 or later (Windows reference: 1.99.1).' >&2; exit 1
fi
download_root="$project_root/artifacts/core-prototype/downloads"
upstream_root="$proto_root/upstream"
mkdir -p -- "$download_root" "$upstream_root" "$project_root/assets"
fetch_extract() {
    local archive_name="$1" expected="$2" url="$3" target_parent="$4" expected_dir="$5"
    local archive="$download_root/$archive_name"
    if [[ ! -f "$archive" ]]; then curl --fail --location --proto '=https' --tlsv1.2 --output "$archive" "$url"; fi
    printf '%s  %s\n' "$expected" "$archive" | sha256sum --check --status || { echo "Checksum mismatch: $archive_name" >&2; exit 1; }
    if [[ ! -d "$target_parent/$expected_dir" ]]; then unzip -q "$archive" -d "$target_parent"; fi
}
ntscrt_directory="$(node -p 'require(process.argv[1]).ntscrt.directory' "$proto_root/upstream-sources.json")"
ntscrt_vendor="$proto_root/vendor/$ntscrt_directory"
engine_source="$upstream_root/$ntscrt_directory"
if [[ ! -d "$ntscrt_vendor" ]]; then echo "Missing checked-in NTSCRT sources: $ntscrt_vendor" >&2; exit 1; fi
if [[ ! -d "$engine_source" ]]; then cp -R -- "$ntscrt_vendor" "$upstream_root"; fi
fetch_extract librashader-76462c03.zip 48bf30a981b91d284f9dfd2df53ea227c7420cf4c15e40e7cd0d7b99ec385739 \
    https://codeload.github.com/SnowflakePowered/librashader/zip/76462c030b75c4f2d56e5386c3d4d7d1128318b8 \
    "$upstream_root" librashader-76462c030b75c4f2d56e5386c3d4d7d1128318b8
fetch_extract shaders.zip e219396f38deec9a762fb6ef1c5d690fcd36e90e64e66a1c782fbb717f92820e \
    https://github.com/libretro/slang-shaders/archive/cb01f2f238700eed7ec1c859be1737a9fbcdcb7c.zip \
    "$project_root/assets" slang-shaders-cb01f2f238700eed7ec1c859be1737a9fbcdcb7c
shader_source="$upstream_root/librashader-76462c030b75c4f2d56e5386c3d4d7d1128318b8"
apply_prepared_patch() {
    local source_root="$1" patch_file="$2"
    if ! GIT_CEILING_DIRECTORIES="$upstream_root" git -C "$source_root" apply --reverse --check "$patch_file" 2>/dev/null; then
        GIT_CEILING_DIRECTORIES="$upstream_root" git -C "$source_root" apply "$patch_file"
    fi
}
apply_prepared_patch "$shader_source" "$engine_source/port/librashader-wgpu-push-constants.patch"
export CARGO_TARGET_DIR="$project_root/artifacts/core-prototype/target"
apply_prepared_patch "$engine_source" "$proto_root/engine-adapter/host-lifecycle.patch"
export CARGO_BUILD_JOBS="${CARGO_BUILD_JOBS:-2}"
export CMAKE_GENERATOR=Ninja
export CMAKE_POLICY_VERSION_MINIMUM=3.5
export NTSCRT_BACKEND="${NTSCRT_BACKEND:-gl}"
result_dir="$project_root/artifacts/core-prototype/linux-$(date -u +%Y%m%d-%H%M%S)"
mkdir -p -- "$result_dir"
{
    uname -a
    cat /etc/os-release
    rustc --version
    cargo --version
    node --version
    "$ffmpeg_tool" -version
    printf 'Backend requested: %s\n' "$NTSCRT_BACKEND"
} > "$result_dir/environment.txt"
cargo build --release --locked --manifest-path "$proto_root/Cargo.toml" 2>&1 | tee "$result_dir/build.log"
cargo test --release --locked --manifest-path "$proto_root/Cargo.toml" -p tapetube-digital-host 2>&1 | tee "$result_dir/digital-tests.log"
# Same external consumer as Windows, when .NET 10 SDK is available; missing SDK is recorded.
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks | grep -q '^10\.'; then
    dotnet build "$proto_root/abi-smoke/AbiSmoke.csproj" -c Release -o "$project_root/artifacts/core-prototype/abi-smoke" --nologo
    dotnet "$project_root/artifacts/core-prototype/abi-smoke/TapeTube.DigitalAbiSmoke.dll" \
        "$CARGO_TARGET_DIR/release/libtapetube_digital_host.so" > "$result_dir/digital-abi-tests.json" 2> "$result_dir/digital-abi-tests.log"
else
    printf '{"executed":false,"reason":"No .NET 10 SDK; external ABI consumer not run"}\n' > "$result_dir/digital-abi-tests.json"
fi
node "$proto_root/test.mjs" --output "$result_dir/rendering" 2>&1 | tee "$result_dir/rendering.log"
printf '\nLinux core results: %s\n' "$result_dir"
