#!/usr/bin/env bash
# Downloads the bge-small-en-v1.5 ONNX model + tokenizer vocab into src/gateway/Models
# (git-ignored). Run once before first `dotnet run` or `dotnet test`.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$ROOT/src/gateway/Models/bge-small-en-v1.5"
BASE="https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main"

mkdir -p "$DEST"

download() {
  local file="$1"
  if [ ! -f "$DEST/$file" ]; then
    echo "Downloading $file ..."
    curl -sL --fail -o "$DEST/$file" "$BASE/$file"
  else
    echo "$file already present"
  fi
}

download "onnx/model_quantized.onnx"
download "vocab.txt"

echo "NLP model assets ready at $DEST"
