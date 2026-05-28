#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_DYLIB="${ROOT_DIR}/build/opencvsharp-extern-4.13/OpenCvSharpExtern/libOpenCvSharpExtern.dylib"
BUNDLE_DIR="${ROOT_DIR}/build/opencvsharp-extern-4.13-self-contained"

if [[ ! -f "${SOURCE_DYLIB}" ]]; then
  echo "Missing native library: ${SOURCE_DYLIB}" >&2
  exit 1
fi

rm -rf "${BUNDLE_DIR}"
mkdir -p "${BUNDLE_DIR}"

list_dependencies() {
  otool -L "$1" | awk 'NR > 2 { print $1 }'
}

is_system_dependency() {
  case "$1" in
    /usr/lib/*|/System/Library/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

copy_dependency() {
  local dylib="$1"
  local target="${BUNDLE_DIR}/$(basename "${dylib}")"
  local resolved_dependency

  if [[ -f "${target}" ]]; then
    return
  fi

  cp "${dylib}" "${target}"
  chmod u+w "${target}"

  while IFS= read -r dependency; do
    resolved_dependency="$(resolve_dependency "${dylib}" "${dependency}")"
    [[ -n "${resolved_dependency}" ]] || continue
    copy_dependency "${resolved_dependency}"
  done < <(list_dependencies "${dylib}")
}

resolve_dependency() {
  local from_dylib="$1"
  local dependency="$2"

  if is_system_dependency "${dependency}"; then
    return
  fi

  if [[ "${dependency}" == /opt/homebrew/* ]]; then
    if [[ ! -f "${dependency}" ]]; then
      echo "Missing dependency: ${dependency}" >&2
      exit 1
    fi

    printf '%s\n' "${dependency}"
    return
  fi

  if [[ "${dependency}" == @loader_path/* ]]; then
    local loader_candidate
    loader_candidate="$(cd "$(dirname "${from_dylib}")" && cd "$(dirname "${dependency#@loader_path/}")" 2>/dev/null && pwd)/$(basename "${dependency}")"
    if [[ -f "${loader_candidate}" ]]; then
      printf '%s\n' "${loader_candidate}"
      return
    fi

    echo "Unable to resolve dependency ${dependency} from ${from_dylib}" >&2
    exit 1
  fi

  if [[ "${dependency}" != @rpath/* ]]; then
    echo "Unsupported dependency ${dependency} from ${from_dylib}" >&2
    exit 1
  fi

  local dependency_name="${dependency#@rpath/}"
  local rpath candidate

  while IFS= read -r rpath; do
    case "${rpath}" in
      @loader_path)
        candidate="$(dirname "${from_dylib}")/${dependency_name}"
        ;;
      @loader_path/*)
        candidate="$(cd "$(dirname "${from_dylib}")" && cd "${rpath#@loader_path/}" 2>/dev/null && pwd)/${dependency_name}"
        ;;
      /opt/homebrew/*)
        candidate="${rpath}/${dependency_name}"
        ;;
      *)
        echo "Unsupported rpath ${rpath} in ${from_dylib}" >&2
        exit 1
        ;;
    esac

    if [[ -f "${candidate}" ]]; then
      printf '%s\n' "${candidate}"
      return
    fi
  done < <(otool -l "${from_dylib}" | awk '/LC_RPATH/{flag=1} flag && /path /{print $2; flag=0}')

  echo "Unable to resolve dependency ${dependency} from ${from_dylib}" >&2
  exit 1
}

rewrite_dependency() {
  local dylib="$1"
  local dependency="$2"
  local bundled_dependency="${BUNDLE_DIR}/$(basename "${dependency}")"
  local loader_dependency="@loader_path/$(basename "${dependency}")"

  if [[ ! -f "${bundled_dependency}" || "${dependency}" == "${loader_dependency}" ]]; then
    return
  fi

  install_name_tool -change "${dependency}" "${loader_dependency}" "${dylib}" 2>/dev/null
}

copy_dependency "${SOURCE_DYLIB}"

while IFS= read -r dylib; do
  install_name_tool -id "@loader_path/$(basename "${dylib}")" "${dylib}" 2>/dev/null

  while IFS= read -r dependency; do
    rewrite_dependency "${dylib}" "${dependency}"
  done < <(list_dependencies "${dylib}")
done < <(find "${BUNDLE_DIR}" -maxdepth 1 -type f -name '*.dylib' | sort)

codesign --force --sign - "${BUNDLE_DIR}"/*.dylib >/dev/null 2>/dev/null

echo "Created self-contained native bundle:"
echo "${BUNDLE_DIR}"
echo
echo "Files: $(find "${BUNDLE_DIR}" -maxdepth 1 -type f -name '*.dylib' | wc -l | tr -d ' ')"
du -sh "${BUNDLE_DIR}"
