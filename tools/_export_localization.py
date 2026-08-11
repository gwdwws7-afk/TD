"""Extract localization replacements from TDLocalization.cs and generate JSON."""
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Scripts", "TowerDefense", "TDLocalization.cs")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Localization")

# Parse the ChineseReplacements array from the C# source.
with open(SRC, encoding="utf-8") as f:
    content = f.read()

# Find all new("english", "chinese") pairs
pattern = re.compile(r'new\("((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)"\)')
pairs = pattern.findall(content)

print(f"Extracted {len(pairs)} replacement pairs")

# Build the JSON structure
data = {
    "schemaVersion": 1,
    "languages": {
        "en": {},  # English is the identity (no replacements needed)
        "zh": {},
    }
}

for english, chinese in pairs:
    # Unescape C# string escapes
    en = english.replace('\\"', '"').replace('\\n', '\n').replace('\\\\', '\\')
    zh = chinese.replace('\\"', '"').replace('\\n', '\n').replace('\\\\', '\\')
    data["languages"]["zh"][en] = zh

# Also add the hardcoded ON/OFF specials
data["languages"]["zh"]["ON"] = "开启"
data["languages"]["zh"]["OFF"] = "关闭"

os.makedirs(OUT_DIR, exist_ok=True)
out_path = os.path.join(OUT_DIR, "strings.json")
with open(out_path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2, ensure_ascii=False)

print(f"Written to {out_path}")
print(f"  zh entries: {len(data['languages']['zh'])}")
