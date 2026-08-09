param(
    [string]$OutputDirectory = "E:/TD/Assets/Resources/Art/UI/P12"
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class TDUiAlphaCrop
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
                throw new InvalidOperationException("UI source contains no visible pixels: " + inputPath);
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

$assets = @(
    @{ source = "Assets/Resources/Art/hud_panel_bg.png"; output = "frame_command.png" },
    @{ source = "Assets/Resources/Art/hud_status_strip.png"; output = "frame_compact.png" },
    @{ source = "Assets/Resources/Art/hud_button_restart.png"; output = "frame_action.png" }
)

$results = foreach ($asset in $assets) {
    $sourcePath = Join-Path $projectRoot $asset.source
    $outputPath = Join-Path $OutputDirectory $asset.output
    [TDUiAlphaCrop]::Crop($sourcePath, $outputPath, 4)
    $bitmap = New-Object System.Drawing.Bitmap($outputPath)
    try {
        [ordered]@{
            output = [IO.Path]::GetFullPath($outputPath)
            width = $bitmap.Width
            height = $bitmap.Height
            bytes = (Get-Item -LiteralPath $outputPath).Length
        }
    } finally {
        $bitmap.Dispose()
    }
}

$results | ConvertTo-Json -Depth 4
