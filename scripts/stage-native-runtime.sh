#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

ARCH="${1:-arm64}"

case "${ARCH}" in
  arm64)  RID="osx-arm64" ;;
  x86_64) RID="osx-x64" ;;
  *) echo "Unsupported architecture: ${ARCH}" >&2; exit 1 ;;
esac

LIB_NAME="libOpenCvSharpExtern.dylib"
DEFAULT_SOURCE="${ROOT_DIR}/build/opencvsharp-extern-4.13/opencvsharpextern/${LIB_NAME}"
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
