using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace AutoActions {
    [Serializable]
    public class Configurations : IPluginConfiguration {
        public int Version { get; set; } = 1;

        public bool StopProgram { get; set; } = true;
        public bool PLDChecked { get; set; } = true;
        public bool WARChecked { get; set; } = true;
        public bool DRKChecked { get; set; } = true;
        public bool GNBChecked { get; set; } = true;
        public bool PCTChecked { get; set; } = true;
        public bool DNCChecked { get; set; } = true;
        public bool SMNChecked { get; set; } = true;
        public bool SCHChecked { get; set; } = true;
        public bool SGEChecked { get; set; } = true;
        public bool DungeonChecked { get; set; } = true;
        public bool TrialChecked { get; set; } = false;
        public bool RaidChecked { get; set; } = false;
        public bool AllianceChecked { get; set; } = true;
        public bool MainWindowVisible { get; set; } = false;
        public bool TankStanceEnabled { get; set; } = true;
        public bool ResetList { get; set; } = false;
        public List<string> DncPrio { get; set; } = new List<string> { "SAM", "PCT", "VPR", "NIN", "MNK", "RPR", "BLM", "DRG", "SMN", "RDM", "MCH", "BRD", "DNC" };

        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
