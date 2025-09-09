using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
#if UNITY_EDITOR
public class UWPicker : EditorWindow {
    private uint queuedEventId, playingEventId;
    private double unloadTime;
    private string queuedEventName, playingEventName, loadedBank, bankToUnload, rtpcName;
    public TreeView eventView, stateView, switchView, triggerView, rtpcView;
    private Label playingEvent, State, currentSwitch, lastTrigger, selectedRTPC, rtpcMin, rtpcMax;
    private Slider rtpcSlider;
    public Button generateSoundbanks, setNone;
    private HashSet<string> eventSet = new();
    private UWBankDictionaries _bankObj;
    private Camera cam;

    [MenuItem("Window/Wwise Picker 2.0 %#w")]
    public static void OpenPicker() {
        GetWindow<UWPicker>("Wwise Picker 2.0", true, typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow"));
    }
    protected virtual void CreateGUI() {
        cam = Camera.main;

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"Assets/UpgradedWwise/UIDocs/{GetType().Name}.uxml");
        if (visualTree == null) {
            Debug.LogError("UI Document not found!");
            return;
        }

        visualTree.CloneTree(rootVisualElement);


        eventView = rootVisualElement.Q<TreeView>("EventView");
        stateView = rootVisualElement.Q<TreeView>("StateView");
        switchView = rootVisualElement.Q<TreeView>("SwitchView");
        triggerView = rootVisualElement.Q<TreeView>("TriggerView");
        rtpcView = rootVisualElement.Q<TreeView>("RTPCView");

        playingEvent = rootVisualElement.Q<Label>("PlayingEvent");
        State = rootVisualElement.Q<Label>("CurrentState");
        currentSwitch = rootVisualElement.Q<Label>("CurrentSwitch");
        lastTrigger = rootVisualElement.Q<Label>("LastTrigger");
        selectedRTPC = rootVisualElement.Q<Label>("SelectedRTPC");
        rtpcMin = rootVisualElement.Q<Label>("SliderMin");
        rtpcMax = rootVisualElement.Q<Label>("SliderMax");

        rtpcSlider = rootVisualElement.Q<Slider>("RTPCSlider");

        generateSoundbanks = rootVisualElement.Q<Button>("GenerateSoundbanks");

        setNone = rootVisualElement.Q<Button>("SetNone");

        string path = Application.dataPath + "/UpgradedWwise/_generated/AudioEnum.cs";
        var enumData = LoadEnumFile(path);

        if (enumData.Count == 0) {
            rootVisualElement.Add(new Label("No enums found in AudioEnum.cs"));
            return;
        }

        if (RefreshTreeView(enumData) == 5) {
            if (_bankObj == null) {
                string[] guids = AssetDatabase.FindAssets($"t:{nameof(UWBankDictionaries)}");

                if (guids.Length > 0) {
                    _bankObj = AssetDatabase.LoadAssetAtPath<UWBankDictionaries>(AssetDatabase.GUIDToAssetPath(guids[0]));
                } else {
                    Debug.LogError("Please create a EventBankDictionary Scriptable Object!");
                    return;
                }
            }

            eventView.selectionChanged += _ => TryQueueEvent();
            rootVisualElement.RegisterCallback<KeyDownEvent>(c => {
                if (c.keyCode == KeyCode.Space) {
                    TryPlayEvent();
                }
            });

            stateView.selectedIndicesChanged += TrySetState;

            switchView.selectedIndicesChanged += TrySetSwitch;

            triggerView.itemsChosen += _ => TrySetTrigger();

            rtpcView.selectionChanged += _ => TrySetRTPC();

            rtpcSlider.RegisterValueChangedCallback(c => {
                if (Enum.TryParse(rtpcName, out AudioRTPC rtpcEnum))
                    AkUnitySoundEngine.SetRTPCValue(rtpcEnum.ToString(), c.newValue, cam.gameObject);
            });

            generateSoundbanks.RegisterCallback<PointerEnterEvent>(_ => generateSoundbanks.style.backgroundColor = new Color(0.15f, 0.15f, 0.25f, 1));
            generateSoundbanks.RegisterCallback<PointerLeaveEvent>(_ => generateSoundbanks.style.backgroundColor = new Color(0.25f, 0.25f, 0.35f, 1));
            generateSoundbanks.clicked += () => {
                if (AkUtilities.IsSoundbankGenerationAvailable(AkWwiseEditorSettings.Instance.WwiseInstallationPath)) {
                    AkUtilities.GenerateSoundbanks(AkWwiseEditorSettings.Instance.WwiseInstallationPath, AkWwiseEditorSettings.WwiseProjectAbsolutePath);
                }

                UWAudioEnumGenerator.GenerateEnum();
                RefreshTreeView(enumData);
            };
        }
    }
    private int RefreshTreeView(Dictionary<string, List<string>> enumData) {
        int fullView = 0;

        if (eventView != null) {
            InitializeTreeView(eventView, enumData, 0);
            fullView++;
        }

        if (stateView != null) {
            InitializeTreeView(stateView, enumData, 1);
            fullView++;
        }
        if (switchView != null) {
            InitializeTreeView(switchView, enumData, 2);
            fullView++;
        }
        if (triggerView != null) {
            InitializeTreeView(triggerView, enumData, 3);
            fullView++;
        }
        if (rtpcView != null) {
            InitializeTreeView(rtpcView, enumData, 4);
            fullView++;
        }

        return fullView;
    }
    private void InitializeTreeView(TreeView viewer, Dictionary<string, List<string>> enumData, int index) {
        viewer.SetRootItems(GetTreeRoots(enumData, index));
        viewer.makeItem = () => new Label();
        viewer.bindItem = (VisualElement element, int index) =>
            (element as Label).text = viewer.GetItemDataForIndex<string>(index);
    }
    private void TryQueueEvent() {
        if (eventView.selectedItem != null) {
            string eventName = eventView.selectedItem.ToString();

            if (_bankObj.EventBankDict.TryGetValue(eventName, out var eventInstance)) {
                AkBankManager.LoadBank(eventInstance.bankName, false, false);

                if (loadedBank != eventInstance.bankName && !string.IsNullOrEmpty(loadedBank)) {
                    bankToUnload = loadedBank;
                    unloadTime = EditorApplication.timeSinceStartup + 5.0f;
                }

                loadedBank = eventInstance.bankName;
                queuedEventId = eventInstance.id;
                queuedEventName = eventName;
            }
        }
    }
    private void TryPlayEvent() {
        if (playingEventId != 0) {
            AkUnitySoundEngine.StopPlayingID(playingEventId);
            playingEvent.text = "Now Playing: \n None";
            playingEventId = 0;

            if (playingEventName == eventView.selectedItem.ToString()) {
                return;
            }
        }

        if (queuedEventId != 0) {
            playingEventId = AkUnitySoundEngine.PostEvent(queuedEventId, cam.gameObject);
            playingEventName = queuedEventName;
            playingEvent.text = $"Now Playing: \n {playingEventName.Replace("start", string.Empty).Replace("play", string.Empty)}";
        }
    }
    private void UnloadBank(string bankToUnload) {
        if (loadedBank != bankToUnload)
            AkBankManager.UnloadBank(bankToUnload);
    }
    private void Update() {
        if (unloadTime > 0 && EditorApplication.timeSinceStartup >= unloadTime) {
            UnloadBank(bankToUnload);
            unloadTime = -1;
        }
    }
    private void TrySetState(IEnumerable<int> selectedIndices) {
        var selectedItems = stateView.GetSelectedItems<string>();
        bool setNewState = false;

        if (selectedItems != null) {
            foreach (var selectedItem in selectedItems) {
                if (selectedItem.children.ToList().Count() == 0) {
                    int parentId = stateView.GetParentIdForIndex(selectedIndices.First());

                    AkUnitySoundEngine.SetState(stateView.GetItemDataForId<string>(parentId), selectedItem.data);

                    State.text = $"Current State: \n {stateView.GetItemDataForId<string>(parentId)} -> {selectedItem.data}";
                    setNewState = true;
                }
            }
        }

        if (!setNewState) {
            State.text = "Current State: \n None";
        }
    }
    private void TrySetSwitch(IEnumerable<int> selectedIndices) {
        var selectedItems = switchView.GetSelectedItems<string>();
        bool setNewSwitch = false;

        if (selectedItems != null) {
            foreach (var selectedItem in selectedItems) {
                if (selectedItem.children.ToList().Count() == 0) {
                    int parentId = switchView.GetParentIdForIndex(selectedIndices.First());

                    AkUnitySoundEngine.SetSwitch(switchView.GetItemDataForId<string>(parentId), selectedItem.data, cam.gameObject);

                    currentSwitch.text = $"Current Switch: \n {switchView.GetItemDataForId<string>(parentId)} -> {selectedItem.data}";
                    setNewSwitch = true;
                }
            }
        }

        if (!setNewSwitch) {
            currentSwitch.text = "Current Switch: \n None";
        }
    }
    private void TrySetTrigger() {
        if (triggerView.selectedItem != null && Enum.TryParse(triggerView.selectedItem.ToString(), out AudioTrigger triggerToSet)) {
            AkUnitySoundEngine.PostTrigger(triggerToSet.ToString(), cam.gameObject);
        }
    }
    private void TrySetRTPC() {
        if (rtpcView.selectedItem != null && _bankObj.RTPCBankDict.TryGetValue(rtpcView.selectedItem.ToString(), out var rtpcInstance)) {
            selectedRTPC.text = $"Selected RTPC: \n {rtpcView.selectedItem.ToString().Replace("_", " ")}";
            rtpcMin.text = rtpcInstance.min.ToString();
            rtpcMax.text = rtpcInstance.max.ToString();

            rtpcName = rtpcView.selectedItem.ToString();

            rtpcSlider.value = rtpcInstance.min + ((rtpcInstance.max - rtpcInstance.min) / 2);
        }
    }
    private Dictionary<string, List<string>> LoadEnumFile(string filePath) {
        var result = new Dictionary<string, List<string>>();
        if (!File.Exists(filePath)) {
            Debug.LogError("AudioEnum.cs file not found!");
            return result;
        }

        string[] lines = File.ReadAllLines(filePath);
        Regex enumRegex = new(@"public\s+enum\s+(\w+)");
        string currentEnum = null;

        foreach (string line in lines) {
            string trimmed = line.Trim();

            Match match = enumRegex.Match(trimmed);
            if (match.Success) {
                currentEnum = match.Groups[1].Value.Replace("Audio", string.Empty);
                currentEnum += currentEnum.Contains("Switch") ? "es" : "s";
                result[currentEnum] = new List<string>();
                continue;
            }

            if (string.IsNullOrEmpty(currentEnum)) continue;

            if (trimmed.StartsWith("}")) {
                currentEnum = null;
                continue;
            }

            if (!trimmed.Contains("=") && !trimmed.EndsWith(",") && !trimmed.EndsWith("}")) continue;

            string valueName = trimmed.Split(new[] { '=', ',' })[0].Trim();
            if (valueName != "{")
                result[currentEnum].Add(valueName);
        }

        return result;
    }


    private IList<TreeViewItemData<string>> GetTreeRoots(Dictionary<string, List<string>> enumData, int index) {
        int id = 0;

        var childNodes = new List<TreeViewItemData<string>>();

        var wwuData = AkWwiseProjectInfo.GetData();

        switch (enumData.Keys.ToList()[index]) {
            case "Events":
                var returnEventData = BuildTreeFromWorkUnits(
                    wwuData.EventWwu, enumData.Keys.ToList()[index], id);

                childNodes.AddRange(returnEventData.treeData);
                break;
            case "States":
                var returnStateData = BuildTreeFromWorkUnits(
                    wwuData.StateWwu, enumData.Keys.ToList()[index], id);

                childNodes.AddRange(returnStateData.treeData);
                break;
            case "Switches":
                var returnSwitchData = BuildTreeFromWorkUnits(
                    wwuData.SwitchWwu, enumData.Keys.ToList()[index], id);

                childNodes.AddRange(returnSwitchData.treeData);
                break;
            case "Triggers":
                var returnTriggerData = BuildTreeFromWorkUnits(
                    wwuData.TriggerWwu, enumData.Keys.ToList()[index], id);

                childNodes.AddRange(returnTriggerData.treeData);
                break;
            case "RTPCs":
                var returnRtpcData = BuildTreeFromWorkUnits(
                    wwuData.RtpcWwu, "Game Parameters", id);

                childNodes.AddRange(returnRtpcData.treeData);
                break;
        }

        return childNodes;
    }


    private (List<TreeViewItemData<string>> treeData, int newId)
    BuildTreeFromWorkUnits<T>(IEnumerable<AkWwiseProjectData.GenericWorkUnit<T>> workUnitList, string rootCategory, int currentId)
    where T : AkWwiseProjectData.AkInformation {
        var sortedWorkUnits = workUnitList
        .OrderByDescending(w => w.ParentPath.Count(c => c == '/'))
        .ToList();

        var workUnitMap = new Dictionary<string, (TreeViewItemData<string> data, bool topLevel)>();
        var childNodes = new List<TreeViewItemData<string>>();

        foreach (var workUnit in sortedWorkUnits) {
            string workUnitPath = GetWorkUnitPath(workUnit.ParentPath, rootCategory);
            int depth = workUnitPath.Select((c, i) => (c, i)).Count(ci => ci.c == '/' && ci.i + 1 < workUnitPath.Length && char.IsUpper(workUnitPath[ci.i + 1]));
            string workUnitName = GetLastName(workUnitPath);

            var eventChildren = new List<TreeViewItemData<string>>();

            if (workUnit is AkWwiseProjectData.GenericWorkUnit<AkWwiseProjectData.GroupValue> groupWorkUnit) {
                foreach (var groupEntry in groupWorkUnit.List) {
                    var treeGroupList = new List<TreeViewItemData<string>>();
                    foreach (var value in groupEntry.values) {
                        treeGroupList.Add(new TreeViewItemData<string>(currentId++, value.Name));
                    }

                    eventChildren.Add(new TreeViewItemData<string>(currentId++, groupEntry.Name, treeGroupList));
                }
            } else {
                foreach (var eventEntry in workUnit.List) {
                    eventSet.Add(eventEntry.Name);
                    eventChildren.Add(new TreeViewItemData<string>(currentId++, eventEntry.Name));
                }
            }

            var workUnitNode = new TreeViewItemData<string>(currentId++, workUnitName, eventChildren);
            workUnitMap[workUnitPath] = (workUnitNode, depth == 0);
        }

        foreach (var workUnit in sortedWorkUnits) {
            string workUnitPath = GetWorkUnitPath(workUnit.ParentPath, rootCategory);
            string parentPath = GetParentWorkUnitPath(workUnitPath);

            if (parentPath != null && workUnitMap.TryGetValue(parentPath, out var parentNode)) {

                var updatedChildren = parentNode.data.children.ToList();
                updatedChildren.Add(workUnitMap[workUnitPath].data);
                var updatedParentNode = new TreeViewItemData<string>(parentNode.data.id, parentNode.data.data, updatedChildren);

                workUnitMap[parentPath] = (updatedParentNode, workUnitMap[parentPath].topLevel);
            }
        }
        foreach (var updatedWorkUnit in workUnitMap.Values) {
            if (updatedWorkUnit.topLevel) {
                childNodes.Add(updatedWorkUnit.data);
            }
        }

        return (childNodes, currentId);
    }
    private string GetWorkUnitPath(string rawPath, string rootCategory) {
        string workUnitPath = rawPath.Replace($"{rootCategory}/Default Work Unit/", string.Empty);
        var parts = workUnitPath.Split('/')
            .Where(seg => !string.IsNullOrEmpty(seg) && char.IsUpper(seg[0]));

        return string.Join("/", parts);
    }

    private string GetParentWorkUnitPath(string path) {
        var segments = path.Split('/');
        for (int i = segments.Length - 2; i >= 0; i--) {
            if (!string.IsNullOrEmpty(segments[i]) && char.IsUpper(segments[i][0])) {
                return string.Join("/", segments.Take(i + 1));
            }
        }
        return null;
    }

    private string GetLastName(string path) {
        var match = Regex.Match(path, @"[^/]+$");
        return match.Success ? match.Value : null;
    }

    private void OnEnable() {
        EditorApplication.update += Update;
    }

    private void OnDisable() {
        EditorApplication.update -= Update;
    }
}
public abstract class UWPopupPicker : UWPicker {
    protected SerializedProperty property;
    protected virtual TreeView treeView => GetTreeView();

    protected static void OpenPicker<T>(SerializedProperty property, Rect buttonRect) where T : UWPopupPicker {
        var window = GetWindow<T>($"Wwise {typeof(T).Name.Replace("UW", "").Replace("Picker", "")} Picker", true);

        window.minSize = new Vector2(buttonRect.width, 400);
        window.maxSize = new Vector2(buttonRect.width, 400);
        window.property = property;

        Vector2 buttonScreenPos = GUIUtility.GUIToScreenPoint(new Vector2(buttonRect.x, buttonRect.y));
        Vector2 position = ClampToScreen(new Rect(buttonScreenPos.x, buttonScreenPos.y + buttonRect.height + 25, buttonRect.width, 400));

        window.position = new Rect(position.x, position.y, buttonRect.width, 400);
    }

    private static Vector2 ClampToScreen(Rect windowRect) {
        float screenWidth = Screen.currentResolution.width;
        float screenHeight = Screen.currentResolution.height;

        float x = Mathf.Clamp(windowRect.x, 0, screenWidth - windowRect.width);
        float y = Mathf.Clamp(windowRect.y, 0, screenHeight - windowRect.height);

        return new Vector2(x, y);
    }

    protected virtual void SetItem<TEnum>() where TEnum : struct, Enum {
        if (Enum.TryParse(treeView.selectedItem.ToString(), out TEnum selectedItem)) {
            property.enumValueFlag = Convert.ToInt32(selectedItem);
            property.serializedObject.ApplyModifiedProperties();
            Close();
        }
    }

    protected virtual void SetNone<TEnum>() where TEnum : struct, Enum {
        property.enumValueFlag = 0;
        property.serializedObject.ApplyModifiedProperties();
        Close();
    }

    protected virtual TreeView GetTreeView() {
        return eventView;
    }
}

public class UWEventPicker : UWPopupPicker {
    protected override TreeView treeView => eventView;
    public static void OpenEventPicker(SerializedProperty property, Rect buttonRect) {
        OpenPicker<UWEventPicker>(property, buttonRect);
    }

    protected override void CreateGUI() {
        base.CreateGUI();

        treeView.selectionChanged += _ => SetItem<AudioEvent>();
        setNone.clicked += SetNone<AudioEvent>;
    }
}

public class UWStatePicker : UWPopupPicker {
    protected override TreeView treeView => stateView;
    public static void OpenStatePicker(SerializedProperty property, Rect buttonRect) {
        OpenPicker<UWStatePicker>(property, buttonRect);
    }

    protected override void CreateGUI() {
        base.CreateGUI();
        treeView.selectionChanged += _ => SetItem<AudioState>();
    }

    protected override void SetItem<TEnum>() {
        var selectedItems = treeView.GetSelectedItems<string>();
        foreach (var selectedItem in selectedItems) {
            if (selectedItem.children.Count() == 0) {
                int parentId = treeView.GetParentIdForIndex(treeView.selectedIndices.First());
                string fullName = $"{treeView.GetItemDataForId<string>(parentId)}_BREAK_{treeView.selectedItem}";

                if (Enum.TryParse(fullName, out TEnum selectedState)) {
                    property.enumValueFlag = Convert.ToInt32(selectedState);
                    property.serializedObject.ApplyModifiedProperties();
                    Close();
                }
            }
        }
    }
}

public class UWSwitchPicker : UWStatePicker {
    protected override TreeView treeView => switchView;
    public static void OpenSwitchPicker(SerializedProperty property, Rect buttonRect) {
        OpenPicker<UWSwitchPicker>(property, buttonRect);
    }

    protected override void CreateGUI() {
        base.CreateGUI();
        treeView.selectionChanged += _ => SetItem<AudioSwitch>();
    }
}

public class UWTriggerPicker : UWPopupPicker {
    protected override TreeView treeView => triggerView;
    public static void OpenTriggerPicker(SerializedProperty property, Rect buttonRect) {
        OpenPicker<UWTriggerPicker>(property, buttonRect);
    }

    protected override void CreateGUI() {
        base.CreateGUI();
        treeView.selectionChanged += _ => SetItem<AudioTrigger>();
    }

    protected override TreeView GetTreeView() => triggerView;
}

public class UWRTPCPicker : UWPopupPicker {
    protected override TreeView treeView => rtpcView;
    public static void OpenRTPCPicker(SerializedProperty property, Rect buttonRect) {
        OpenPicker<UWRTPCPicker>(property, buttonRect);
    }

    protected override void CreateGUI() {
        base.CreateGUI();
        treeView.selectionChanged += _ => SetItem<AudioRTPC>();
    }
}
#endif