using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoActions {
    [Serializable]
    public class Configurations : IPluginConfiguration {
        public int Version { get; set; }

        public bool StopProgram { get; set; }
        public bool PLDChecked { get; set; }
        public bool WARChecked { get; set; }
        public bool DRKChecked { get; set; }
        public bool GNBChecked { get; set; }
        public bool PCTChecked { get; set; }
        public bool DNCChecked { get; set; }
        public bool SMNChecked { get; set; }
        public bool SCHChecked { get; set; }
        public bool SGEChecked { get; set; }
        public bool TankDungeonChecked { get; set; }
        public bool TankTrialChecked { get; set; }
        public bool TankRaidChecked { get; set; }
        public bool TankAllianceChecked { get; set; }
        public bool DPSDungeonChecked { get; set; }
        public bool DPSTrialChecked { get; set; }
        public bool DPSRaidChecked { get; set; }
        public bool DPSAllianceChecked { get; set; }
        public bool HealerDungeonChecked { get; set; }
        public bool HealerTrialChecked { get; set; }
        public bool HealerRaidChecked { get; set; }
        public bool HealerAllianceChecked { get; set; }
        public bool MainWindowVisible { get; set; }
        public bool TankStanceEnabled { get; set; }
        public bool ResetList { get; set; }

        [JsonInclude]
        public List<string> DncPrio { get; set; } = new List<string>();

        public Configurations() {
            Version = 1;

            StopProgram = true;
            PLDChecked = true;
            WARChecked = true;
            DRKChecked = true;
            GNBChecked = true;
            PCTChecked = true;
            DNCChecked = true;
            SMNChecked = true;
            SCHChecked = true;
            SGEChecked = true;
            TankDungeonChecked = true;
            TankTrialChecked = false;
            TankRaidChecked = false;
            TankAllianceChecked = true;
            DPSDungeonChecked = true;
            DPSTrialChecked = true;
            DPSRaidChecked = true;
            DPSAllianceChecked = true;
            HealerDungeonChecked = true;
            HealerTrialChecked = true;
            HealerRaidChecked = true;
            HealerAllianceChecked = true;
            MainWindowVisible = false;
            TankStanceEnabled = true;
            ResetList = false;
        }

        public void Save() {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
