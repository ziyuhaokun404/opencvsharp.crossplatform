#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

detect_rid() {
  local os arch
  os="$(uname -s)"
  arch="$(uname -m)"

  case "${os}" in
    Darwin)
      case "${arch}" in
        arm64) echo "osx-arm64" ;;
        x86_64) echo "osx-x64" ;;
        *) echo "Unsupported macOS architecture: ${arch}" >&2; exit 1 ;;
      esac
      ;;
    Linux)
      case "${arch}" in
        aarch64|arm64) echo "linux-arm64" ;;
        x86_64) echo "linux-x64" ;;
        *) echo "Unsupported Linux architecture: ${arch}" >&2; exit 1 ;;
      esac
      ;;
    MINGW*|MSYS*|CYGWIN*|Windows_NT)
      case "${arch}" in
        ARM64|aarch64|arm64) echo "win-arm64" ;;
        AMD64|x86_64) echo "win-x64" ;;
        *) echo "Unsupported Windows architecture: ${arch}" >&2; exit 1 ;;
      esac
      ;;
    *)
      echo "Unsupported OS: ${os}" >&2
      exit 1
      ;;
  esac
}

detect_library_name() {
  case "$1" in
    win-*) echo "OpenCvSharpExtern.dll" ;;
    osx-*) echo "libOpenCvSharpExtern.dylib" ;;
    linux-*) echo "libOpenCvSharpExtern.so" ;;
    *)
      echo "Unsupported RID: $1" >&2
      exit 1
      ;;
  esac
}

RID="${1:-$(detect_rid)}"
LIB_NAME="$(detect_library_name "${RID}")"
DEFAULT_SOURCE="${ROOT_DIR}/build/opencvsharp-extern-4.13/OpenCvSharpExtern/${LIB_NAME}"
SOURCE_PATH="${2:-${OPENCVSHARP_EXTERN_PATH:-${DEFAULT_SOURCE}}}"
TARGET_DIR="${ROOT_DIR}/build/native/${RID}"
TARGET_PATH="${TARGET_DIR}/${LIB_NAME}"

if [[ ! -f "${SOURCE_PATH}" ]]; then
  echo "Missing native library: ${SOURCE_PATH}" >&2
  echo "Set OPENCVSHARP_EXTERN_PATH or pass a path as the second argument if your output is elsewhere." >&2
  exit 1
fi

mkdir -p "${TARGET_DIR}"
cp "${SOURCE_PATH}" "${TARGET_PATH}"

echo "Staged native runtime:"
echo "  RID:    ${RID}"
echo "  Source: ${SOURCE_PATH}"
echo "  Target: ${TARGET_PATH}"
