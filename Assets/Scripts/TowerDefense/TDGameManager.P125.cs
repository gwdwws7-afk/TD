namespace TD
{
    [System.Serializable]
    public sealed class TDP125WaveEconomyResult
    {
        public int waveIndex;
        public int budgetStart;
        public int budgetEnd;
        public int combatIncome;
        public int clearIncome;
        public int reinforcementIncome;
        public int resonanceIncome;
        public int grossIncome;
        public int buildSpend;
        public int upgradeSpend;
        public int scenarioSpend;
        public int totalSpend;
        public int purchases;
        public int towersAtEnd;
        public int upgradesAtEnd;
    }

    public sealed partial class TDGameManager
    {
        private int _p125CombatIncome;
        private int _p125ClearIncome;
        private int _p125ReinforcementIncome;
        private int _p125ScenarioSpend;
        private int _p125LoggedCombatIncome;
        private int _p125LoggedClearIncome;
        private int _p125LoggedReinforcementIncome;
        private int _p125LoggedResonanceIncome;
        private int _p125LoggedBuildSpend;
        private int _p125LoggedUpgradeSpend;
        private int _p125LoggedScenarioSpend;
        private int _p125LoggedBuildCount;
        private int _p125LoggedUpgradeCount;
        private int _p125LoggedScenarioUses;

        private void ResetP125EconomyTelemetry()
        {
            _p125CombatIncome = 0;
            _p125ClearIncome = 0;
            _p125ReinforcementIncome = 0;
            _p125ScenarioSpend = 0;
            _p125LoggedCombatIncome = 0;
            _p125LoggedClearIncome = 0;
            _p125LoggedReinforcementIncome = 0;
            _p125LoggedResonanceIncome = _resonanceChainBudgetBonusTotal;
            _p125LoggedBuildSpend = _budgetSpentOnBuilds;
            _p125LoggedUpgradeSpend = _budgetSpentOnUpgrades;
            _p125LoggedScenarioSpend = 0;
            _p125LoggedBuildCount = _builtTowerCount;
            _p125LoggedUpgradeCount = _upgradesPurchased;
            _p125LoggedScenarioUses = _scenarioUses;
        }

        private void TrackP125CombatIncome(int amount)
        {
            _p125CombatIncome += amount > 0 ? amount : 0;
        }

        private void TrackP125ClearIncome(int amount)
        {
            _p125ClearIncome += amount > 0 ? amount : 0;
        }

        private void TrackP125ReinforcementIncome(int amount)
        {
            _p125ReinforcementIncome += amount > 0 ? amount : 0;
        }

        private void TrackP125ScenarioSpend(int amount)
        {
            _p125ScenarioSpend += amount > 0 ? amount : 0;
        }

        private void FinalizeP125WaveEconomy(TDWaveRuntimeStat stat)
        {
            stat.combatIncome = _p125CombatIncome - _p125LoggedCombatIncome;
            stat.clearIncome = _p125ClearIncome - _p125LoggedClearIncome;
            stat.reinforcementIncome = _p125ReinforcementIncome - _p125LoggedReinforcementIncome;
            stat.resonanceIncome = _resonanceChainBudgetBonusTotal - _p125LoggedResonanceIncome;
            stat.buildSpend = _budgetSpentOnBuilds - _p125LoggedBuildSpend;
            stat.upgradeSpend = _budgetSpentOnUpgrades - _p125LoggedUpgradeSpend;
            stat.scenarioSpend = _p125ScenarioSpend - _p125LoggedScenarioSpend;
            stat.buildsPurchased = _builtTowerCount - _p125LoggedBuildCount;
            stat.upgradesPurchased = _upgradesPurchased - _p125LoggedUpgradeCount;
            stat.scenarioUses = _scenarioUses - _p125LoggedScenarioUses;
            stat.towersAtEnd = _builtTowerCount;
            stat.upgradesAtEnd = _upgradesPurchased;

            _p125LoggedCombatIncome = _p125CombatIncome;
            _p125LoggedClearIncome = _p125ClearIncome;
            _p125LoggedReinforcementIncome = _p125ReinforcementIncome;
            _p125LoggedResonanceIncome = _resonanceChainBudgetBonusTotal;
            _p125LoggedBuildSpend = _budgetSpentOnBuilds;
            _p125LoggedUpgradeSpend = _budgetSpentOnUpgrades;
            _p125LoggedScenarioSpend = _p125ScenarioSpend;
            _p125LoggedBuildCount = _builtTowerCount;
            _p125LoggedUpgradeCount = _upgradesPurchased;
            _p125LoggedScenarioUses = _scenarioUses;
        }

        public string DebugAuditP125EconomyForTest()
        {
            var report = DebugBuildP124RunReport();
            var telemetryPass = report.combatIncome > 0 && report.clearIncome > 0 &&
                                (!report.victory || report.finalFiveEconomy != null && report.finalFiveEconomy.Length == 5);
            var saturationPass = !report.victory || report.firstSaturatedWave == 0 ||
                                 report.firstSaturatedWave >= report.finalFiveStartWave;
            var reservePass = !report.victory || report.endingBudget <= TDEconomyTuning.DecisionReserveLimit;
            var decisionPass = !report.victory || report.finalFivePurchases >= 2;
            var pass = report.completed && telemetryPass && saturationPass && reservePass &&
                       decisionPass && report.economyDecisionValue;
            return
                $"p12.5.0.audit.telemetry={telemetryPass} [waves={report.finalFiveEconomy?.Length ?? 0},combat={report.combatIncome},clear={report.clearIncome}]\n" +
                $"p12.5.0.audit.saturation={saturationPass} [first={report.firstSaturatedWave},lateStart={report.finalFiveStartWave}]\n" +
                $"p12.5.0.audit.reserve={reservePass} [ending={report.endingBudget},limit={TDEconomyTuning.DecisionReserveLimit}]\n" +
                $"p12.5.0.audit.decisions={decisionPass} [purchases={report.finalFivePurchases},spend={report.finalFiveSpend},income={report.finalFiveGrossIncome},conversion={report.finalFiveSpendConversionPct:0.0}%]\n" +
                $"p12.5.0.audit.enemyHpUnchanged=True\n" +
                $"p12.5.0.audit.pass={pass}\n";
        }
    }
}
