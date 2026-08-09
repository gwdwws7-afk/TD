param(
    [switch]$DryRun,
    [string]$Model = "gpt-image-2",
    [string]$Quality = "high"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "td_imagegen_common.ps1")

$uv = Get-Command uv -ErrorAction Stop
$imageCli = Resolve-TDImageGenCli
$chromaCli = Join-Path (Split-Path $imageCli -Parent) "remove_chroma_key.py"
Import-TDOpenAIApiKey -Required (-not $DryRun.IsPresent)

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$rawDirectory = Join-Path $projectRoot "output\imagegen\p12_ui_skin"
$assetDirectory = Join-Path $projectRoot "Assets\Resources\Art\UI\P12"
New-Item -ItemType Directory -Path $rawDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null

$master = @"
Use case: stylized-concept
Asset type: production UI frame asset for a top-down dieselpunk tower-defense game
Primary request: create one isolated interface frame for Emberline Frontier, a rail-defense command system built from worn black iron, gunmetal, dark smoked instrument glass, riveted corners, restrained brass fittings, tiny ember-orange pilot lights, and sparse cyan gauge illumination
Style/medium: premium hand-painted 2D game UI, crisp readable silhouette, subtle material depth, shipping-quality asset, cohesive with an arid industrial railway frontier
Lighting/mood: restrained warm ember glow against cool steel shadow; functional military engineering, not fantasy ornament
Color palette: coal black, gunmetal gray, tarnished brass, ember orange, sparse instrument cyan
Materials/textures: riveted steel, enamel warning paint, soot, scratched brass, smoked glass
Constraints: one centered frame only; perfectly flat solid #00ff00 chroma-key outside the frame; no cast shadow outside the frame; no text, letters, numbers, logos, symbols, watermark, characters, scenery, weapons, creatures, loose props, or detached pieces; keep the border narrow so the interior remains usable; preserve clean corners and strong readability at small game-UI scale; do not use #00ff00 in the frame
Avoid: cyberpunk neon, steampunk filigree, ornate fantasy carving, excessive rust, excessive bloom, thick decorative borders, noisy micro-detail, gradients in the chroma-key background
"@

$assets = @(
    @{
        Name = "frame_command"
        Detail = "Composition: wide command-console frame with an exact visual aspect ratio near 3.3:1, armored upper-left identity bracket, compact corner bolts, thin lower rail, and a large dark readable interior. The entire frame nearly fills the image width while keeping generous flat green padding outside."
    },
    @{
        Name = "frame_alert"
        Detail = "Composition: very wide low-profile emergency broadcast frame with an exact visual aspect ratio near 5.5:1, red-orange enamel warning tabs at both ends, reinforced black iron brackets, and a dark readable interior. Keep it authoritative and compact, like a railway signal box alarm."
    },
    @{
        Name = "frame_action"
        Detail = "Composition: compact horizontal action-button bezel with an exact visual aspect ratio near 3.2:1, chamfered iron corners, one small amber pilot lamp, a thin brass lower lip, and a dark readable center. It must remain legible around 150 by 46 pixels."
    },
    @{
        Name = "frame_control_rail"
        Detail = "Composition: very wide shallow operator control rail with an exact visual aspect ratio near 7:1, segmented steel mounting slots, restrained brass separators, tiny cyan and amber indicator lights, and a dark readable interior. It must remain clear around 300 by 44 pixels."
    }
)

if (-not $DryRun) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class TDAlphaCrop
{
    public static void Crop(string inputPath, string outputPath, int padding)
    {
        using (var source = new Bitmap(inputPath))
        {
            int minX = source.Width;
            int minY = source.Height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    if (source.GetPixel(x, y).A <= 8)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                throw new InvalidOperationException("Generated UI frame contains no visible pixels.");
            }

            minX = Math.Max(0, minX - padding);
            minY = Math.Max(0, minY - padding);
            maxX = Math.Min(source.Width - 1, maxX + padding);
            maxY = Math.Min(source.Height - 1, maxY + padding);
            var bounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            using (var cropped = source.Clone(bounds, PixelFormat.Format32bppArgb))
            {
                cropped.Save(outputPath, ImageFormat.Png);
            }
        }
    }
}
"@
}

foreach ($asset in $assets) {
    $rawPath = Join-Path $rawDirectory ($asset.Name + "_raw.png")
    $alphaPath = Join-Path $rawDirectory ($asset.Name + "_alpha.png")
    $finalPath = Join-Path $assetDirectory ($asset.Name + ".png")
    $prompt = $master + "`n" + $asset.Detail
    $arguments = @(
        $imageCli,
        "generate",
        "--prompt", $prompt,
        "--model", $Model,
        "--size", "1536x1024",
        "--quality", $Quality,
        "--background", "opaque",
        "--output-format", "png",
        "--out", $rawPath,
        "--force"
    )
    if ($DryRun) {
        $arguments += "--dry-run"
    }

    Write-Host "[image2.0] Generating $($asset.Name) with $Model ..."
    $generated = $false
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        & $uv.Source run --with openai --with pillow python @arguments
        if ($LASTEXITCODE -eq 0) {
            $generated = $true
            break
        }

        if ($attempt -lt 3) {
            Write-Warning "Image API attempt $attempt failed for $($asset.Name); retrying in 6 seconds."
            Start-Sleep -Seconds 6
        }
    }
    if (-not $generated) {
        throw "Image 2.0 generation failed after 3 attempts for $($asset.Name)."
    }

    if ($DryRun) {
        continue
    }

    & $uv.Source run --with openai --with pillow python $chromaCli `
        --input $rawPath `
        --out $alphaPath `
        --auto-key border `
        --soft-matte `
        --transparent-threshold 12 `
        --opaque-threshold 220 `
        --despill `
        --force
    if ($LASTEXITCODE -ne 0) {
        throw "Chroma removal failed for $($asset.Name)."
    }

    [TDAlphaCrop]::Crop($alphaPath, $finalPath, 8)
    if (-not (Test-Path -LiteralPath $finalPath) -or (Get-Item -LiteralPath $finalPath).Length -lt 4096) {
        throw "Generated UI asset is missing or too small: $finalPath"
    }
}

Write-Host "P12 UI skin assets written to $assetDirectory"
