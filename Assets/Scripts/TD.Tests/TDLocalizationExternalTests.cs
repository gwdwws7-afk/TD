using System.Text.RegularExpressions;
using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Release-engineering pin: localization lives in
    /// Resources/Localization/strings.json (the runtime loader prefers it;
    /// the in-code array is only the missing-file fallback). These pins hold
    /// the file's integrity so "add a language = edit a file" can't silently
    /// regress to baked code strings.
    /// </summary>
    public class TDLocalizationExternalTests
    {
        private static string LoadJsonText()
        {
            var asset = Resources.Load<TextAsset>("Localization/strings");
            Assert.IsNotNull(asset, "strings.json must exist under Resources/Localization");
            return asset.text;
        }

        [Test]
        public void JsonContainsTheFullPairSet()
        {
            // The generated file is indent-2 JSON: every zh pair is a line of
            // the shape   "EN text": "CN text",   — count those.
            var json = LoadJsonText();
            var inZh = false;
            var pairs = 0;
            foreach (var rawLine in json.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("\"zh\":"))
                {
                    inZh = true;
                    continue;
                }

                if (inZh)
                {
                    if (line.StartsWith("\"en\":") || (line.StartsWith("\"") && line.Contains("\": \"")))
                    {
                        pairs++;
                    }
                    else if (line == "}" || line == "},")
                    {
                        break;
                    }
                }
            }

            Assert.GreaterOrEqual(pairs, 556, $"zh pairs >= 556 (552 before the four expansion towers synced), got {pairs}");
        }

        [Test]
        public void ExpansionTowerNamesAreLocalized()
        {
            var json = LoadJsonText();
            StringAssert.Contains("\"Slag Burner\"", json);
            StringAssert.Contains("炉渣喷灯", json);
            StringAssert.Contains("\"Salvage Derrick\"", json);
            StringAssert.Contains("捞轨吊机", json);
            StringAssert.Contains("\"Rail Barricade\"", json);
            StringAssert.Contains("轨障车", json);
            StringAssert.Contains("\"Long Rail Cannon\"", json);
            StringAssert.Contains("远程轨道炮", json);
        }

        [Test]
        public void CoreStringsSurviveExternalization()
        {
            var json = LoadJsonText();
            StringAssert.Contains("\"Rail Lancer\"", json);
            StringAssert.Contains("轨枪塔", json);
            StringAssert.Contains("余烬铁道", json);
        }
    }
}
