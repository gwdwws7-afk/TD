"""Quick test: call image_gen.py edit for one frame with a simple prompt."""
import os
import sys
import subprocess
import time

image_gen = os.path.expanduser("~/.codex/skills/.system/imagegen/scripts/image_gen.py")
print(f"image_gen: {image_gen}")
print(f"exists: {os.path.exists(image_gen)}")

cmd = [
    sys.executable, image_gen, "edit",
    "--model", "gpt-image-1.5",
    "--image", r"E:\TD\Assets\Resources\Art\anim\tower_rail_lancer_03.png",
    "--prompt", "small red apple on white background",
    "--size", "1024x1024",
    "--background", "transparent",
    "--output-format", "png",
    "--out", r"E:\TD\output\imagegen\_t2_raw\test_apple.png",
    "--force",
]
print("cmd:", cmd)
print("starting...")
t0 = time.time()
try:
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=90)
    print(f"done in {time.time()-t0:.1f}s, exit={r.returncode}")
    print("STDOUT:", r.stdout[-500:])
    print("STDERR:", r.stderr[-500:])
except subprocess.TimeoutExpired:
    print(f"TIMEOUT after 90s")
