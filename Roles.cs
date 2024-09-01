using System.Collections.Generic;

public static class Roles {
    public static readonly List<string> Healers = new List<string> {
        "SGE",
        "WHM",
        "SCH",
        "AST"
    };
    public static readonly List<string> Tanks = new List<string> {
        "PLD",
        "WAR",
        "GNB",
        "DRK"
    };
    public static readonly List<string> DPS = new List<string> {
        "SAM",
        "MNK",
        "DRG",
        "NIN",
        "RPR",
        "VPR",
        "BRD",
        "MCH",
        "DNC",
        "PCT",
        "BLM",
        "SMN",
        "RDM",
    };
    public static string GetRole(string jobAbr) {
        if (Healers.Contains(jobAbr)) {
            return "Healer";
        }
        if (Tanks.Contains(jobAbr)) {
            return "Tank";
        }
        if (DPS.Contains(jobAbr)) {
            return "DPS";
        }
        return "Unknown";
    }
}