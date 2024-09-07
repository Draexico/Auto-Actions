﻿using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using System;
using Lumina.Excel.GeneratedSheets;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects.Types;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Common.Component.Excel;
using Dalamud.Game.ClientState.Resolvers;

namespace AutoActions {
    public sealed class Plugin : IDalamudPlugin {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
        [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] internal static IDutyState DutyState { get; private set; } = null!;

        private const string CommandName = "/aa";
        private const string CommandOpenUI = "/aaui";
        private bool mainWindowVisible = false;
        public Configurations Configuration { get; init; }
        private float recastTime = 0;
        private bool isStanceOn = false;
        private bool isDPS = false;
        private bool isHealer = false;
        private bool isInView = false;
        private string jobAbr = "";
        private string dutyType = "";
        private Dictionary<string, JobActions> jobActionsDict = new Dictionary<string, JobActions>();
        private Dictionary<string, ActiveJobActions> activeJobActionsDict = new Dictionary<string, ActiveJobActions>();

        public Plugin() {
            Configuration = PluginInterface.GetPluginConfig() as Configurations ?? new Configurations();

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
                HelpMessage = "Enable Auto Actions"
            });
            CommandManager.AddHandler(CommandOpenUI, new CommandInfo(OpenUI) {
                HelpMessage = "Open Auto Actions Settings"
            });
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            ReadJsonFile();
            GetCurrentJob();
            if (Configuration.StopProgram) {
                ClientState.TerritoryChanged += OnTerritoryChanged;
                ClientState.ClassJobChanged += OnClassJobChanged;
            } else {
                Framework.Update -= OnFrameUpdate;
                ClientState.TerritoryChanged -= OnTerritoryChanged;
                ClientState.ClassJobChanged -= OnClassJobChanged;
            }

        }

        public void Dispose() {
            CommandManager.RemoveHandler(CommandName);
            Framework.Update -= OnFrameUpdate;
            ClientState.TerritoryChanged -= OnTerritoryChanged;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        }

        private void OnCommand(string command, string args) {
            Configuration.StopProgram = !Configuration.StopProgram;
            if (Configuration.StopProgram) {
                ChatGui.Print("Auto Actions Enabled");
                ClientState.TerritoryChanged += OnTerritoryChanged;
            } else {
                ChatGui.Print("Auto Actions Disabled");
                Framework.Update -= OnFrameUpdate;
                ClientState.TerritoryChanged -= OnTerritoryChanged;
            }
            Configuration.Save();
        }
        private void OpenUI(string command, string args) {
            OpenConfigUi();
        }

        private void ReadJsonFile() {
            try {
                FileInfo assemblyLocation = PluginInterface.AssemblyLocation;
                if (assemblyLocation.Directory != null) {
                    string directoryPath = assemblyLocation.Directory.FullName;
                    string jobActions = Path.Combine(directoryPath, "jobActions.json");
                    string activeJobActions = Path.Combine(directoryPath, "activeActions.json");
                    if (File.Exists(jobActions)) {
                        string jsonString = File.ReadAllText(jobActions);
                        var deserializedDict = JsonSerializer.Deserialize<Dictionary<string, JobActions>>(jsonString);
                        if (deserializedDict != null) {
                            jobActionsDict = deserializedDict;
                        } else {
                            ChatGui.Print("Failed to deserialize JSON.");
                        }
                    } else {
                        ChatGui.Print("JSON file not found.");
                    }
                    if (File.Exists(activeJobActions)) {
                        string jsonString = File.ReadAllText(activeJobActions);
                        var deserializedDict = JsonSerializer.Deserialize<Dictionary<string, ActiveJobActions>>(jsonString);
                        if (deserializedDict != null) {
                            activeJobActionsDict = deserializedDict;
                        } else {
                            ChatGui.Print("Failed to deserialize JSON..");
                        }
                    } else {
                        ChatGui.Print("JSON file not found");
                    }
                }
            } catch (Exception ex) {
                ChatGui.Print($"Error reading JSON file: {ex.Message}");
            }
        }
        public class JobActions {
            public List<int> jobActions { get; set; } = new List<int>();
        }
        public class ActiveJobActions {
            public int activeJobActions { get; set; }
        }
        private async void GetCurrentJob() {
            await Task.Delay(50);  // Short delay before checking the new job
            var localPlayerCharacterObject = ClientState.LocalPlayer as ICharacter;
            if (localPlayerCharacterObject != null) {
                var jobSheet = DataManager.GetExcelSheet<ClassJob>();
                if (jobSheet != null) {
                    var classJob = localPlayerCharacterObject.ClassJob;
                    var jobId = classJob.Id;
                    var jobRow = jobSheet.GetRow(jobId);
                    if (jobRow != null) {
                        jobAbr = jobRow.Abbreviation;
                        var role = Roles.GetRole(jobAbr);
                        if (IsChecked(jobAbr)) {
                            if (role == "Tank") {
                                CheckTankStance(jobAbr);
                            } else if (role == "Healer") {
                                isHealer = true;
                            } else if (role == "DPS") {
                                isDPS = true;
                            }
                        }
                    }
                } else {
                    ChatGui.Print("Job Sheet not found.");
                }
            }
        }

        private void OnTerritoryChanged(ushort newTerritoryType) {
            if (IsInDuty(newTerritoryType)) {
                // Check the duty type and corresponding configuration before subscribing to the event
                if ((dutyType == "Dungeon" && Configuration.TankDungeonChecked ) ||
                    (dutyType == "Trial" && Configuration.TankTrialChecked) ||
                    (dutyType == "Raid" && Configuration.TankRaidChecked) ||
                    (dutyType == "Alliance Raid" && Configuration.TankAllianceChecked)) {
                        Framework.Update += OnFrameUpdate;
                        DutyState.DutyWiped += OnDutyWiped;
                        DutyState.DutyStarted += OnDutyRecommenced;
                }
            } else {
                isStanceOn = false;
                Framework.Update -= OnFrameUpdate;
                DutyState.DutyWiped -= OnDutyWiped;
                DutyState.DutyStarted -= OnDutyRecommenced;
            }
        }

        // Check if player moved to a duty instance
        private bool IsInDuty(ushort territory) {

            var territoryType = DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territory);
            if (territoryType == null)
                return false;

            var contentFinderCondition = territoryType.ContentFinderCondition?.Value;
            if (contentFinderCondition == null)
                return false;

            // Determine the type of duty based on ContentType.Row
            switch (contentFinderCondition.ContentType.Row)
            {
                case 2:
                    dutyType = "Dungeon";
                    return true;
                case 4:
                    dutyType = "Trial";
                    return true;
                case 5:
                    dutyType = "Raid";
                    return true;
                case 7:
                    dutyType = "Alliance Raid";
                    return true;
                default:
                    return false;
            }
        }
        private void OnClassJobChanged(uint classJobId) {
            GetCurrentJob();
        }

        private void OnFrameUpdate(IFramework framework) {
            var localPlayerCharacterObject = ClientState.LocalPlayer as ICharacter;
            var localPlayerGameObject = ClientState.LocalPlayer as IGameObject;

            /*var length = PartyList.Length;
            if (length > 0 && localPlayerGameObject != null) {
                Dictionary<string, string> partyMembersJobs = new();
                for (int i = 0; i < length; i++) {
                    var partyMember = PartyList[i];
                    if (partyMember != null && partyMember.ObjectId != localPlayerGameObject.GameObjectId) {
                        var jobId = partyMember.ClassJob.Id;
                        var jobSheet = DataManager.GetExcelSheet<ClassJob>();
                        if (jobSheet != null) {
                            var jobRow = jobSheet.GetRow(jobId);
                            uint partyMemberObjectId = partyMember.ObjectId;
                            if (jobRow != null) {
                                jobAbr = jobRow.Abbreviation;
                                partyMembersJobs[partyMember.Name.ToString()] = jobAbr;
                            }
                        } else {
                            ChatGui.Print("Job Sheet not found.");
                        }
                    }
                }
            }*/
            
            // While loading into duty, check until skills becomes available
            if (localPlayerGameObject != null) {
                unsafe {
                    GameObject* playerObject = (GameObject*)localPlayerGameObject.Address;
                    if (playerObject != null) {
                        TargetSystem* targetSystem = TargetSystem.Instance();
                        isInView = targetSystem->IsObjectOnScreen(playerObject);
                        if (isDPS && isInView) {
                            if (jobActionsDict.TryGetValue(jobAbr, out var jobActionList)) {
                                for (int i = 0; i < jobActionList.jobActions.Count; i++) {
                                    unsafe {
                                        ActionManager* actions = ActionManager.Instance();
                                        var act = actions->GetActionStatus(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, (uint)jobActionList.jobActions[i]);
                                        if (act == 572 || act == 0) {
                                            isDPS = false;
                                            UseActions(jobAbr);
                                        }
                                    }
                                }
                            }
                        }
                        // While loading into duty, check until stance becomes available
                        else if (!isStanceOn && isInView) {
                            if (jobActionsDict.TryGetValue(jobAbr, out var jobActionList)) {
                                for (int i = 0; i < jobActionList.jobActions.Count; i++) {
                                    unsafe {
                                        ActionManager* actions = ActionManager.Instance();
                                        var act = actions->GetActionStatus(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, (uint)jobActionList.jobActions[i]);
                                        if (act == 0) {
                                            isStanceOn = true;
                                            UseActions(jobAbr);
                                        }
                                    }
                                }
                            }
                        } else if (isHealer && isInView) {
                            if (jobActionsDict.TryGetValue(jobAbr, out var jobActionList)) {
                                if (jobAbr == "SGE" && (dutyType == "Dungeon" || dutyType == "Alliance Raid")) {
                                    for (int i = 0; i < jobActionList.jobActions.Count; i++) {
                                        // TODO: Select tank
                                    }
                                } else if (jobAbr == "SCH") {
                                    for (int i = 0; i < jobActionList.jobActions.Count; i++) {
                                        unsafe {
                                            ActionManager* actions = ActionManager.Instance();
                                            var act = actions->GetActionStatus(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, (uint)jobActionList.jobActions[i]);
                                            if (act == 0) {
                                                isHealer = false;
                                                UseActions(jobAbr);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    } else {
                        ChatGui.Print("Player object not found.");
                    }
                }
            }
        }

        private void OnDutyWiped(object? sender, ushort args) {
            isInView = false;
            Framework.Update += OnFrameUpdate;
        }
        private void OnDutyRecommenced(object? sender, ushort args) {
            isInView = false;
            Framework.Update -= OnFrameUpdate;
        }
        public bool IsChecked(string jobAbr) {
            string propertyName = $"{jobAbr}Checked";
            var property = Configuration.GetType().GetProperty(propertyName);
            if (property != null) {
                var value = property.GetValue(Configuration);
                if (value is bool booleanValue) {
                    return booleanValue;
                }
            }
            return false;
        }
        public void CheckTankStance(string jobAbr) { 
            // Add each status to a list, then check if stance is on
            var localPlayer = ClientState.LocalPlayer;
            if (localPlayer != null) {
                // Add active statuses to list to check if stance is already on
                var statusList = localPlayer.StatusList;
                var activeStatusIds = new List<uint>();
                for (int i = 0; i < statusList.Length; i++) {
                    var status = statusList[i];
                    if (status != null) {
                        var statusId = status.StatusId;
                        if (statusId != 0) {
                            activeStatusIds.Add(statusId);
                        }
                    }
                }
                // Check if stance is already on
                string stanceKey = $"{jobAbr}_STANCE";
                if (activeJobActionsDict.TryGetValue(stanceKey, out var stanceAction)) {
                    if (activeStatusIds.Contains((uint)stanceAction.activeJobActions)) {
                        isStanceOn = true;
                        return;
                    }
                }
                
            }
        }
        private async void UseActions(string jobAbr) {
            if (jobActionsDict.TryGetValue(jobAbr, out var jobActionList)) {
                for (int i = 0; i < jobActionList.jobActions.Count; i++) {
                    unsafe {
                        ActionManager* actions = ActionManager.Instance();
                        actions->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, (uint)jobActionList.jobActions[i], 0xE0000000uL);
                        recastTime = actions->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, (uint)jobActionList.jobActions[i]);
                    }
                    await Task.Delay((int)(recastTime * 1000));
                }
                Framework.Update -= OnFrameUpdate;
                if (!isHealer) {
                    isHealer = true;
                } else if (!isDPS) {
                    isDPS = true;
                }
            }
        }
        public void TargetPartyMember(uint partyMemberObjectId) {
            unsafe {
                TargetSystem* target = TargetSystem.Instance();
                TargetPartyMember(partyMemberObjectId);
                var member = ObjectTable.SearchById(partyMemberObjectId);
                if (member != null) {
                    unsafe {
                        var targetSystem = TargetSystem.Instance();
                        if (targetSystem != null) {
                            targetSystem->Target = (GameObject*)member.Address;
                            ChatGui.Print($"Targeted party member: {member.Name}");
                        } else {
                            ChatGui.Print("Could not retrieve TargetSystem instance.");
                        }
                    }
                } else {
                    ChatGui.Print($"Could not find party member with ObjectId: {partyMemberObjectId}");
                }
            }
        }
        private void DrawUI() {
            if (mainWindowVisible) {
                ImGui.SetNextWindowSize(new Vector2(500, 500), ImGuiCond.Once);
                ImGui.Begin("Auto Job Actions", ref mainWindowVisible);

                bool stopProgram = Configuration.StopProgram;
                if (ImGui.Checkbox("Enable Plugin", ref stopProgram)) {
                    Configuration.StopProgram = stopProgram;
                    Configuration.Save();
                    if (Configuration.StopProgram) {
                        ChatGui.Print("Auto Actions Enabled");
                    } else {
                        ChatGui.Print("Auto Actions Disabled");
                    }
                }

                ImGui.Separator();
                if (ImGui.CollapsingHeader("Tank Stance")) {
                    bool pldChecked = Configuration.PLDChecked;
                    bool warChecked = Configuration.WARChecked;
                    bool drkChecked = Configuration.DRKChecked;
                    bool gnbChecked = Configuration.GNBChecked;
                    if (ImGui.Checkbox("Warrior", ref warChecked)) Configuration.WARChecked = warChecked;
                    if (ImGui.Checkbox("Gunbreaker", ref gnbChecked)) Configuration.GNBChecked = gnbChecked;
                    if (ImGui.Checkbox("Dark Knight", ref drkChecked)) Configuration.DRKChecked = drkChecked;
                    if (ImGui.Checkbox("Paladin", ref pldChecked)) Configuration.PLDChecked = pldChecked;

                    ImGui.Separator();
                    bool tankDungeonChecked = Configuration.TankDungeonChecked;
                    bool tankTrialChecked = Configuration.TankTrialChecked;
                    bool tankRaidChecked = Configuration.TankRaidChecked;
                    bool tankAllianceChecked = Configuration.TankAllianceChecked;
                    if (ImGui.Checkbox("Tank Dungeon", ref tankDungeonChecked)) Configuration.TankDungeonChecked = tankDungeonChecked;
                    if (ImGui.Checkbox("Tank Trial", ref tankTrialChecked)) Configuration.TankTrialChecked = tankTrialChecked;
                    if (ImGui.Checkbox("Tank Raid", ref tankRaidChecked)) Configuration.TankRaidChecked = tankRaidChecked;
                    if (ImGui.Checkbox("Tank Alliance", ref tankAllianceChecked)) Configuration.TankAllianceChecked = tankAllianceChecked;

                    Configuration.Save();
                }

                ImGui.Separator();
                if (ImGui.CollapsingHeader("Healers")) {
                    bool schChecked = Configuration.SCHChecked;
                    bool sgeChecked = Configuration.SGEChecked;

                    DrawDPSCheckbox("Scholar", ref schChecked, "Automatically pop fairy on duty start");
                    DrawDPSCheckbox("Sage", ref sgeChecked, "Automatically uses kardia on the tank (dungeon and alliance only)");

                    Configuration.SCHChecked = schChecked;
                    Configuration.SGEChecked = sgeChecked;

                    ImGui.Separator();
                    bool healerDungeonChecked = Configuration.HealerDungeonChecked;
                    bool healerTrialChecked = Configuration.HealerTrialChecked;
                    bool healerRaidChecked = Configuration.HealerRaidChecked;
                    bool healerAllianceChecked = Configuration.HealerAllianceChecked;
                    if (ImGui.Checkbox("Healer Dungeon", ref healerDungeonChecked)) Configuration.HealerDungeonChecked = healerDungeonChecked;
                    if (ImGui.Checkbox("Healer Trial", ref healerTrialChecked)) Configuration.HealerTrialChecked = healerTrialChecked;
                    if (ImGui.Checkbox("Healer Raid", ref healerRaidChecked)) Configuration.HealerRaidChecked = healerRaidChecked;
                    if (ImGui.Checkbox("Healer Alliance", ref healerAllianceChecked)) Configuration.HealerAllianceChecked = healerAllianceChecked;

                    Configuration.Save();
                }

                ImGui.Separator();
                if (ImGui.CollapsingHeader("DPS")) {
                    bool pctChecked = Configuration.PCTChecked;
                    bool smnChecked = Configuration.SMNChecked;
                    bool dncChecked = Configuration.DNCChecked;

                    DrawDPSCheckbox("Pictomancer", ref pctChecked, "Automatically pop motifs on duty start & wipe");
                    DrawDPSCheckbox("Summoner", ref smnChecked, "Automatically pop carbuncle on duty start & wipe");
                    DrawDPSCheckbox("Dancer", ref dncChecked, "Dance partner the DPS following the Dancer Priority List");

                    Configuration.PCTChecked = pctChecked;
                    Configuration.SMNChecked = smnChecked;
                    Configuration.DNCChecked = dncChecked;
                    ImGui.Separator();
                    bool dpsDungeonChecked = Configuration.DPSDungeonChecked;
                    bool dpsTrialChecked = Configuration.DPSTrialChecked;
                    bool dpsRaidChecked = Configuration.DPSRaidChecked;
                    bool dpsAllianceChecked = Configuration.DPSAllianceChecked;
                    if (ImGui.Checkbox("DPS Dungeon", ref dpsDungeonChecked)) Configuration.DPSDungeonChecked = dpsDungeonChecked;
                    if (ImGui.Checkbox("DPS Trial", ref dpsTrialChecked)) Configuration.DPSTrialChecked = dpsTrialChecked;
                    if (ImGui.Checkbox("DPS Raid", ref dpsRaidChecked)) Configuration.DPSRaidChecked = dpsRaidChecked;
                    if (ImGui.Checkbox("DPS Alliance", ref dpsAllianceChecked)) Configuration.DPSAllianceChecked = dpsAllianceChecked;

                    Configuration.Save();
                }

                ImGui.Separator();

                if (ImGui.CollapsingHeader("Show Dancer Priority List")) {
                    if (ImGui.Button("Reset Priority List")) {
                        ResetPriorityList();
                    }
                    int maxPrioCount = 13;  // Limit the list

                    for (int i = 0; i < Configuration.DncPrio.Count && i < maxPrioCount; i++) {
                        ImGui.PushID(i);
                        string currentPos = (i + 1).ToString();
                        ImGui.SetNextItemWidth(80);
                        if (ImGui.BeginCombo($"##{Configuration.DncPrio[i]}", currentPos)) {
                            for (int j = 1; j <= maxPrioCount; j++) {  // Limit to 13 items.
                                if (ImGui.Selectable(j.ToString())) {
                                    int newIndex = j - 1;
                                    if (newIndex != i) {
                                        var item = Configuration.DncPrio[i];
                                        Configuration.DncPrio.RemoveAt(i);
                                        Configuration.DncPrio.Insert(newIndex, item);
                                    }
                                }
                            }
                            ImGui.EndCombo();
                        }
                        ImGui.SameLine();
                        ImGui.Text(Configuration.DncPrio[i]);
                        ImGui.PopID();
                    }
                    Configuration.Save();
                }
                
                ImGui.End();
            }
        }

        private void DrawDPSCheckbox(string label, ref bool isChecked, string description) {
            ImGui.Checkbox(label, ref isChecked);
            ImGui.Indent(20); 
            ImGui.TextDisabled(description);
            ImGui.Unindent(20);
        }

        private void ResetPriorityList() {
            Configuration.DncPrio = new List<string> { "SAM", "PCT", "VPR", "NIN", "MNK", "RPR", "BLM", "DRG", "SMN", "RDM", "MCH", "BRD", "DNC" };
            Configuration.Save();
        }

        private void OpenConfigUi() {
            mainWindowVisible = true;
        }

        public string Name => "Auto Job Actions";
    }
}
