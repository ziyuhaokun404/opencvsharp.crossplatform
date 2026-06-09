#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="${ROOT_DIR}/build/opencvsharp-extern-4.13"
SOURCE_DIR="${ROOT_DIR}/ref/opencvsharp-4.13/src"
OPEN_CV_DIR_DEFAULT="/opt/homebrew/Cellar/opencv/4.13.0_10/lib/cmake/opencv4"
OPEN_CV_DIR="${OpenCV_DIR:-${OPEN_CV_DIR_DEFAULT}}"
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
echo "  ${BUILD_DIR}/OpenCvSharpExtern/libOpenCvSharpExtern.dylib"
