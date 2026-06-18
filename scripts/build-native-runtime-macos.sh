#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="${ROOT_DIR}/build/opencvsharp-extern-4.13"
SOURCE_DIR="${ROOT_DIR}/ref/opencvsharp-4.13/src"
if [[ -z "${OpenCV_DIR:-}" ]]; then
  if command -v brew >/dev/null 2>&1; then
    OPEN_CV_PREFIX="$(brew --prefix opencv)"
    OPEN_CV_DIR="${OPEN_CV_PREFIX}/lib/cmake/opencv4"
  else
    echo "OpenCV_DIR is not set and Homebrew was not found." >&2
    echo "Install OpenCV with Homebrew or run with OpenCV_DIR=/path/to/opencv4/cmake." >&2
    exit 1
  fi
else
  OPEN_CV_DIR="${OpenCV_DIR}"
fi
ARCHITECTURES="${CMAKE_OSX_ARCHITECTURES:-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
PARALLELISM="${CMAKE_BUILD_PARALLEL_LEVEL:-$(sysctl -n hw.ncpu)}"

cmake -S "${SOURCE_DIR}" \
  -B "${BUILD_DIR}" \
  -DOpenCV_DIR="${OPEN_CV_DIR}" \
  -DCMAKE_BUILD_TYPE="${CONFIGURATION}" \
  -DCMAKE_OSX_ARCHITECTURES="${ARCHITECTURES}" \
  -DCMAKE_POLICY_VERSION_MINIMUM=3.5

cmake --build "${BUILD_DIR}" --config "${CONFIGURATION}" --parallel "${PARALLELISM}"

echo
echo "Built native runtime:"
echo "  ${BUILD_DIR}/opencvsharpextern/libOpenCvSharpExtern.dylib"
