using System.Collections.Generic;
using TMPro;
using TowerDefense.Maps;
using TowerDefense.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TowerDefense.Gameplay
{
    /// <summary>Screen-space HUD and pad selection. Gameplay state belongs to LevelSession.</summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        public LevelSession session;
        public Camera gameCamera;
        public Font font; // Retained so existing scene references remain compatible.
        public TMP_FontAsset hudFont;
        public Sprite[] towerIcons;
        public Sprite[] enemyIcons;

        private static readonly Color PanelColor = new Color(0.028f, 0.067f, 0.089f, 0.97f);
        private static readonly Color TextColor = new Color(0.96f, 0.98f, 1f);
        private static readonly Color MutedColor = new Color(0.65f, 0.77f, 0.81f);
        private static readonly Color AccentColor = new Color(0.07f, 0.34f, 0.36f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.43f);
        private static readonly Color HealthColor = new Color(1f, 0.34f, 0.40f);
        private static readonly Color ManaColor = new Color(0.54f, 0.67f, 1f);
        private static readonly Color FireballColor = new Color(1f, 0.38f, 0.10f);
        private static readonly Color SilverColor = new Color(0.76f, 0.86f, 0.96f);
        private static readonly Color PrismaticColor = new Color(0.88f, 0.61f, 1f);
        private static readonly Color FrostSpellColor = new Color(0.40f, 0.89f, 1f);
        private static readonly Color ReinforcementColor = new Color(.43f, 1f, .68f);
        private static readonly Color BattlecryColor = new Color(1f, .67f, .30f);
        private static readonly Color[] TowerColors =
        {
            new Color(1f, 0.75f, 0.28f), new Color(0.39f, 0.88f, 1f), new Color(1f, 0.46f, 0.28f),
            new Color(0.47f, 0.95f, 0.64f), new Color(.87f, .64f, 1f)
        };
        private static readonly string[] TowerNames = { "RAPID FIRE", "FROST", "BLAST", "BARRACKS", "SUPPORT" };
        private static readonly string[] TowerRoles = { "Focus fire", "Slow", "AOE 2.4", "Block + fight", "Weaken + heal" };
        private static readonly string[] EnemyNames = { "GRUNT", "RUNNER", "BRUTE", "SWARM", "BREAKER", "FLYER", "SHAMAN", "WARDEN", "TITAN" };
        private static readonly HudIcon.Symbol[] TowerSymbols =
            { HudIcon.Symbol.Gun, HudIcon.Symbol.Frost, HudIcon.Symbol.Cannon, HudIcon.Symbol.Barracks, HudIcon.Symbol.Support };

        private Canvas canvas;
        private CanvasGroup gameplayInteraction;
        private LocalizedCanvas localizedCanvas;
        private bool settingsSubscribed;
        private LevelSession subscribedSession;
        private BuildSlot selectedSlot;
        private TowerAgent selectedTower;
        private TowerKind selectedKind;
        private LineRenderer rangeCircle;
        private Material rangeMaterial;
        private readonly Vector3[] rangePoints = new Vector3[80];
        private Vector3 previousRangeCenter;
        private float previousRangeRadius = -1f;
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private PointerEventData pointerEventData;
        private EventSystem pointerEventSystem;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI goldText;
        private TextMeshProUGUI healthText;
        private TextMeshProUGUI shieldText;
        private TextMeshProUGUI manaText;
        private TextMeshProUGUI waveText;
        private TextMeshProUGUI enemiesText;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI countdownText;
        private TextMeshProUGUI speedText;
        private TextMeshProUGUI pauseText;
        private TextMeshProUGUI slotTitle;
        private TextMeshProUGUI slotDescription;
        private TextMeshProUGUI buildText;
        private TextMeshProUGUI hintText;
        private TextMeshProUGUI resultTitle;
        private TextMeshProUGUI resultDescription;
        private TextMeshProUGUI fireballPriceText;
        private TextMeshProUGUI fireballStatsText;
        private TextMeshProUGUI fireballModeText;
        private TextMeshProUGUI frostPriceText;
        private TextMeshProUGUI frostStatsText;
        private TextMeshProUGUI frostModeText;
        private Button frostButton;
        private Image frostBackground;
        private Button nextLevelButton;
        private Button firstLevelButton;
        private TextMeshProUGUI nextLevelText;
        private Button speedButton;
        private Button pauseButton;
        private Button buildButton;
        private Button fireballButton;
        private Image fireballBackground;
        private Image progressFill;
        private RectTransform progressTrack;
        private GameObject selectionPanel;
        private GameObject hintPanel;
        private GameObject resultPanel;
        private GameObject augmentPanel;
        private GameObject augmentTooltip;
        private TextMeshProUGUI augmentTitle;
        private TextMeshProUGUI augmentSubtitle;
        private TextMeshProUGUI augmentTooltipTitle;
        private TextMeshProUGUI augmentTooltipDescription;
        private readonly Button[] augmentButtons = new Button[3];
        private readonly Image[] augmentCardBorders = new Image[3];
        private readonly TextMeshProUGUI[] augmentTierLabels = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] augmentNames = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] augmentDescriptions = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] augmentSynergyHints = new TextMeshProUGUI[3];
        private readonly HudIcon[] augmentIcons = new HudIcon[3];
        private readonly HudIcon[] augmentMedallions = new HudIcon[3];
        private readonly GameObject[] augmentRainbowFrames = new GameObject[3];
        private readonly Button[] augmentRefreshButtons = new Button[3];
        private readonly TextMeshProUGUI[] augmentRefreshTexts = new TextMeshProUGUI[3];
        private readonly Image[] selectedAugmentBorders = new Image[3];
        private readonly HudIcon[] selectedAugmentIcons = new HudIcon[3];
        private readonly TextMeshProUGUI[] selectedAugmentNames = new TextMeshProUGUI[3];
        private int blockWorldInputUntilFrame = -1;
        private readonly List<HudIcon> waveMarkers = new List<HudIcon>();
        private readonly Button[] towerButtons = new Button[5];
        private readonly Image[] towerCardBackgrounds = new Image[5];
        private readonly TextMeshProUGUI[] towerPrices = new TextMeshProUGUI[5];
        private readonly GameObject[] towerSelectedLabels = new GameObject[5];
        private readonly Button[] spellUpgradeButtons = new Button[4];
        private readonly TextMeshProUGUI[] spellUpgradeTexts = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] spellTitleTexts = new TextMeshProUGUI[4];
        private Button reinforcementButton;
        private Image reinforcementBackground;
        private TextMeshProUGUI reinforcementPriceText;
        private TextMeshProUGUI reinforcementStatsText;
        private TextMeshProUGUI reinforcementModeText;
        private Button battlecryButton;
        private Image battlecryBackground;
        private TextMeshProUGUI battlecryPriceText;
        private TextMeshProUGUI battlecryStatsText;
        private TextMeshProUGUI battlecryModeText;
        private GameObject bossPanel;
        private TextMeshProUGUI bossText;
        private Image bossHealthFill;
        private bool initialized;
        private int lastCanvasScreenWidth;
        private int lastCanvasScreenHeight;
        private float lastCanvasScale = -1f;
        private bool fireballTargeting;
        private bool frostTargeting;
        private bool reinforcementTargeting;
        private float nextRecruitRefresh;
        private float nextSpellRefresh;
        public bool IsTargetingFireball => fireballTargeting;
        public bool IsTargetingFrost => frostTargeting;
        public bool IsTargetingReinforcements => reinforcementTargeting;
        /// <summary>Keep a closing UI click from also selecting a pad or casting on the map.</summary>
        public void BlockWorldInputForFrames(int frames = 1)
        {
            blockWorldInputUntilFrame = Time.frameCount + Mathf.Max(1, frames);
        }
        private bool IsTargetingSpell => fireballTargeting || frostTargeting || reinforcementTargeting;
        private bool HasAdvancedSpells => session != null && session.SpellUpgradeUnlocked;
        private int AvailableTowerCount => session != null ? Mathf.Clamp(session.AvailableTowerCount, 1, 5) : 3;
        private int AugmentChoiceCount => session != null ? Mathf.Clamp(session.ActiveAugmentChoiceCount, 1, 3) : 2;

        private void Start()
        {
            EnsureInitialized();
            Subscribe();
            Refresh();
        }

        private bool HasValidInterface()
        {
            if (canvas == null || titleText == null || goldText == null || healthText == null || manaText == null || waveText == null ||
                enemiesText == null || progressFill == null || hintText == null || statusText == null ||
                selectionPanel == null || hintPanel == null || resultPanel == null || buildButton == null || slotTitle == null ||
                slotDescription == null || buildText == null || speedButton == null || pauseButton == null || speedText == null ||
                pauseText == null || countdownText == null || resultTitle == null || resultDescription == null ||
                fireballButton == null || fireballPriceText == null || fireballStatsText == null || fireballModeText == null || fireballBackground == null ||
                frostButton == null || frostPriceText == null || frostStatsText == null || frostModeText == null || frostBackground == null ||
                nextLevelButton == null || firstLevelButton == null ||
                shieldText == null || augmentPanel == null || augmentTooltip == null || augmentTitle == null || augmentSubtitle == null ||
                augmentTooltipTitle == null || augmentTooltipDescription == null)
                return false;
            for (int i = 0; i < towerPrices.Length; i++)
                if (towerPrices[i] == null || towerButtons[i] == null || towerCardBackgrounds[i] == null || towerSelectedLabels[i] == null)
                    return false;
            for (int i = 0; i < augmentButtons.Length; i++)
                if (augmentButtons[i] == null || augmentCardBorders[i] == null || augmentTierLabels[i] == null ||
                    augmentNames[i] == null || augmentDescriptions[i] == null || augmentIcons[i] == null || augmentRainbowFrames[i] == null || augmentSynergyHints[i] == null ||
                    augmentRefreshButtons[i] == null || augmentRefreshTexts[i] == null)
                    return false;
            if (HasAdvancedSpells)
            {
                if (reinforcementButton == null || battlecryButton == null || bossPanel == null || bossText == null || bossHealthFill == null)
                    return false;
                for (int i = 0; i < spellUpgradeButtons.Length; i++)
                    if (spellUpgradeButtons[i] == null || spellUpgradeTexts[i] == null || spellTitleTexts[i] == null)
                        return false;
            }
            for (int i = 0; i < selectedAugmentNames.Length; i++)
                if (selectedAugmentNames[i] == null || selectedAugmentBorders[i] == null || selectedAugmentIcons[i] == null)
                    return false;
            if (waveMarkers.Count != (session != null ? Mathf.Max(1, session.WaveCount) : 5))
                return false;
            foreach (HudIcon marker in waveMarkers)
                if (marker == null || marker.canvasRenderer == null)
                    return false;
            return true;
        }

        private void EnsureInitialized()
        {
            if (HasValidInterface())
            {
                EnsureLocalization();
                initialized = true;
                return;
            }
            initialized = false;
            Unsubscribe();
            ClearRuntimeInterface();
            if (session == null)
                session = FindAnyObjectByType<LevelSession>();
            if (gameCamera == null)
                gameCamera = Camera.main;
            if (hudFont == null)
                hudFont = TMP_Settings.defaultFontAsset;
            EnsureEventSystem();
            CreateInterface();
            CreateRangeCircle();
            initialized = true;
            Subscribe();
            Refresh();
        }

        private void ClearRuntimeInterface()
        {
            // Hot reload retains runtime children but not readonly arrays or event delegates.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != "Gameplay HUD" && child.name != "Selected Tower Range")
                    continue;
                child.gameObject.SetActive(false);
                if (child.name == "Selected Tower Range")
                {
                    LineRenderer renderer = child.GetComponent<LineRenderer>();
                    if (rangeMaterial == null && renderer != null)
                        rangeMaterial = renderer.sharedMaterial;
                }
                child.name += " (Rebuilding)";
                Destroy(child.gameObject);
            }
            if (rangeMaterial != null)
                Destroy(rangeMaterial);
            rangeMaterial = null;
            rangeCircle = null;
            canvas = null;
            gameplayInteraction = null;
            localizedCanvas = null;
            lastCanvasScreenWidth = lastCanvasScreenHeight = 0;
            lastCanvasScale = -1f;
            selectedTower = null;
            selectedSlot = null;
            fireballTargeting = false;
            frostTargeting = false;
            reinforcementTargeting = false;
            blockWorldInputUntilFrame = -1;
            previousRangeRadius = -1f;
            waveMarkers.Clear();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || (!initialized && transform.Find("Gameplay HUD") == null))
                return;
            EnsureInitialized();
            if (canvas != null)
                canvas.gameObject.SetActive(true);
            Unsubscribe();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideAugmentTooltip();
            if (canvas != null)
                canvas.gameObject.SetActive(false);
            if (rangeCircle != null)
                rangeCircle.enabled = false;
            fireballTargeting = false;
            frostTargeting = false;
            reinforcementTargeting = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (rangeMaterial != null)
                Destroy(rangeMaterial);
        }

        private void Subscribe()
        {
            if (!settingsSubscribed)
            {
                GameSettings.Changed += RefreshLocalization;
                settingsSubscribed = true;
            }
            if (subscribedSession != session)
            {
                if (subscribedSession != null)
                    subscribedSession.Changed -= Refresh;
                subscribedSession = session;
                if (subscribedSession != null)
                    subscribedSession.Changed += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (settingsSubscribed)
            {
                GameSettings.Changed -= RefreshLocalization;
                settingsSubscribed = false;
            }
            if (subscribedSession != null)
                subscribedSession.Changed -= Refresh;
            subscribedSession = null;
        }

        private void EnsureLocalization()
        {
            if (canvas == null)
                return;
            gameplayInteraction = canvas.GetComponent<CanvasGroup>();
            if (gameplayInteraction == null)
                gameplayInteraction = canvas.gameObject.AddComponent<CanvasGroup>();
            localizedCanvas = canvas.GetComponent<LocalizedCanvas>();
            if (localizedCanvas == null)
                localizedCanvas = canvas.gameObject.AddComponent<LocalizedCanvas>();
        }

        private void RefreshLocalization()
        {
            if (!initialized)
                return;
            Refresh();
            localizedCanvas?.RefreshNow();
            lastCanvasScale = -1f;
        }

        private void Update()
        {
            if (!initialized || session == null)
                return;
            if (!HasValidInterface())
            {
                EnsureInitialized();
                return;
            }
            UpdateAssaultDisplay();
            if (session.FrostSpellUnlocked && frostButton != null)
                RefreshFrostAvailability();
            if (HasAdvancedSpells && Time.unscaledTime >= nextSpellRefresh)
            {
                nextSpellRefresh = Time.unscaledTime + .15f;
                RefreshAdvancedSpells();
                RefreshBossDisplay();
            }
            if (selectedTower != null && selectedTower.kind == TowerKind.Barracks &&
                selectionPanel.activeSelf && Time.unscaledTime >= nextRecruitRefresh)
            {
                nextRecruitRefresh = Time.unscaledTime + .25f;
                RefreshSelection();
            }
            if (session.IsMenuOpen)
            {
                if (rangeCircle != null)
                    rangeCircle.enabled = false;
                return;
            }
            if (session.IsChoosingAugment || Time.frameCount <= blockWorldInputUntilFrame)
                return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && IsTargetingSpell)
                CancelFireballTargeting();
            if (IsFinished())
                return;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectTowerKind(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) SelectTowerKind(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) SelectTowerKind(2);
                else if (keyboard.digit4Key.wasPressedThisFrame) SelectTowerKind(3);
                else if (keyboard.digit5Key.wasPressedThisFrame) SelectTowerKind(4);
            }
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                if (fireballTargeting)
                    CancelFireballTargeting();
                else
                    BeginFireballTargeting();
            }
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && session.FrostSpellUnlocked)
            {
                if (frostTargeting)
                    CancelFireballTargeting();
                else
                    BeginFrostTargeting();
            }
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && session.ReinforcementsUnlocked)
            {
                if (reinforcementTargeting)
                    CancelSpellTargeting();
                else
                    BeginReinforcementTargeting();
            }
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame && session.BattlecryUnlocked)
                CastBattlecry();
            if (mouse == null || gameCamera == null)
                return;
            Vector2 pointerPosition = mouse.position.ReadValue();
            bool overInterface = IsPointerOverInterface(pointerPosition);
            if (IsTargetingSpell)
            {
                Vector3 groundPoint = default;
                bool valid = !overInterface && session.IsCombatRunning && TryGetGroundPoint(pointerPosition, out groundPoint);
                if (rangeCircle != null)
                    rangeCircle.enabled = valid;
                if (!valid)
                    return;
                DrawRangeCircle(groundPoint + Vector3.up * 0.035f,
                    reinforcementTargeting ? session.ReinforcementRadius : frostTargeting ? session.FrostSpellRadius : session.FireballRadius,
                    reinforcementTargeting ? ReinforcementColor : frostTargeting ? FrostSpellColor : new Color(1f, 0.42f, 0.12f, 0.90f));
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    bool cast = reinforcementTargeting ? session.TryCastReinforcements(groundPoint) :
                        frostTargeting ? session.TryCastFrostNova(groundPoint) : session.TryCastFireball(groundPoint);
                    if (cast && (reinforcementTargeting || frostTargeting ||
                        (fireballTargeting && !session.CanCastFireball)))
                    {
                        fireballTargeting = false;
                        frostTargeting = false;
                        reinforcementTargeting = false;
                    }
                    Refresh();
                }
                return;
            }
            if (!mouse.leftButton.wasPressedThisFrame || overInterface)
                return;
            Ray ray = gameCamera.ScreenPointToRay(pointerPosition);
            BuildSlot slot = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Collide))
            {
                slot = hit.collider.GetComponentInParent<BuildSlot>();
                if (slot == null)
                {
                    TowerAgent tower = hit.collider.GetComponentInParent<TowerAgent>();
                    if (tower != null)
                        slot = tower.Slot;
                }
            }
            SelectSlot(slot);
        }

        private void LateUpdate()
        {
            if (!initialized || canvas == null || !canvas.isActiveAndEnabled)
                return;
            if (lastCanvasScreenWidth == Screen.width && lastCanvasScreenHeight == Screen.height &&
                Mathf.Approximately(lastCanvasScale, canvas.scaleFactor))
                return;

            // TMP needs new padding and geometry after the game view changes pixel density.
            // Refresh static and hidden labels too; changing resource numbers already update themselves.
            Canvas.ForceUpdateCanvases();
            foreach (TextMeshProUGUI text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text == null)
                    continue;
                text.UpdateMeshPadding();
                text.SetAllDirty();
                text.ForceMeshUpdate(true, true);
            }
            Canvas.ForceUpdateCanvases();
            lastCanvasScreenWidth = Screen.width;
            lastCanvasScreenHeight = Screen.height;
            lastCanvasScale = canvas.scaleFactor;
        }

        private bool IsPointerOverInterface(Vector2 position)
        {
            EventSystem current = EventSystem.current;
            if (current == null)
                return false;
            if (pointerEventData == null || pointerEventSystem != current)
            {
                pointerEventSystem = current;
                pointerEventData = new PointerEventData(current);
            }
            pointerEventData.Reset();
            pointerEventData.position = position;
            uiRaycastResults.Clear();
            current.RaycastAll(pointerEventData, uiRaycastResults);
            foreach (RaycastResult result in uiRaycastResults)
            {
                if (result.module is GraphicRaycaster && result.gameObject != null && result.gameObject.activeInHierarchy)
                    return true;
            }
            return false;
        }

        /// <summary>Select a slot from map input or an editor play-mode verification.</summary>
        public void SelectSlot(BuildSlot slot)
        {
            if (session != null && (session.IsChoosingAugment || session.IsMenuOpen))
                slot = null;
            if (slot != null)
            {
                fireballTargeting = false;
                frostTargeting = false;
                reinforcementTargeting = false;
            }
            if (selectedSlot != slot)
                selectedTower = null;
            selectedSlot = slot;
            if (initialized)
                Refresh();
        }

        private void SelectTowerKind(int index)
        {
            if (session == null || session.IsChoosingAugment || session.IsMenuOpen || IsFinished() || index < 0 || index >= AvailableTowerCount)
                return;
            fireballTargeting = false;
            frostTargeting = false;
            reinforcementTargeting = false;
            selectedKind = (TowerKind)index;
            previousRangeRadius = -1f;
            Refresh();
        }

        /// <summary>Enter fireball aiming using the spell card or Q. Resource payment stays in LevelSession.</summary>
        public bool BeginFireballTargeting()
        {
            if (!initialized || session == null || !session.CanCastFireball)
            {
                if (initialized)
                    Refresh();
                return false;
            }
            selectedSlot = null;
            selectedTower = null;
            fireballTargeting = true;
            frostTargeting = false;
            reinforcementTargeting = false;
            previousRangeRadius = -1f;
            Refresh();
            return true;
        }

        public void CancelFireballTargeting()
        {
            fireballTargeting = false;
            frostTargeting = false;
            reinforcementTargeting = false;
            if (rangeCircle != null)
                rangeCircle.enabled = false;
            if (initialized)
                Refresh();
        }

        /// <summary>Enter frost aiming using the second-level spell card or E.</summary>
        public bool BeginFrostTargeting()
        {
            if (!initialized || session == null || !session.FrostSpellUnlocked ||
                !session.IsCombatRunning || session.Mana < session.FrostSpellCost || session.FrostSpellCooldownRemaining > 0f)
            {
                if (initialized)
                    Refresh();
                return false;
            }
            selectedSlot = null;
            selectedTower = null;
            fireballTargeting = false;
            frostTargeting = true;
            reinforcementTargeting = false;
            previousRangeRadius = -1f;
            Refresh();
            return true;
        }

        public void CancelSpellTargeting() => CancelFireballTargeting();

        public bool BeginReinforcementTargeting()
        {
            if (!initialized || session == null || !session.CanCastReinforcements)
                return false;
            selectedSlot = null;
            selectedTower = null;
            fireballTargeting = false;
            frostTargeting = false;
            reinforcementTargeting = true;
            previousRangeRadius = -1f;
            Refresh();
            return true;
        }

        private void CastBattlecry()
        {
            if (session == null || !session.CanCastBattlecry)
                return;
            blockWorldInputUntilFrame = Time.frameCount + 1;
            CancelSpellTargeting();
            session.TryCastBattlecry();
            Refresh();
        }

        private void RefreshFrostAvailability()
        {
            float remaining = session.FrostSpellCooldownRemaining;
            frostButton.interactable = session.IsCombatRunning && session.Mana >= session.FrostSpellCost && remaining <= 0f;
            if (IsFinished())
                frostModeText.text = "ASSAULT ENDED";
            else if (session.IsPaused)
                frostModeText.text = "PAUSED";
            else if (remaining > 0f)
                frostModeText.text = $"READY IN {Mathf.CeilToInt(remaining)}s";
            else if (!frostTargeting)
                frostModeText.text = session.Mana < session.FrostSpellCost ? $"NEED {session.FrostSpellCost - session.Mana} MANA" : "[E] AIM";
        }

        private void RefreshAdvancedSpells()
        {
            if (!HasAdvancedSpells || reinforcementButton == null || battlecryButton == null)
                return;
            string[] names = { "FIREBALL", "FROST NOVA", "REINFORCE", "BATTLECRY" };
            for (int i = 0; i < spellUpgradeButtons.Length; i++)
            {
                SpellKind spell = (SpellKind)i;
                int level = session.GetSpellLevel(spell);
                spellTitleTexts[i].text = $"{names[i]} / LV {level}";
                spellUpgradeButtons[i].interactable = session.CanUpgradeSpell(spell);
                int cost = session.GetSpellUpgradeCost(spell);
                spellUpgradeTexts[i].text = level >= 3 ? "MAX LEVEL / LV 3" :
                    $"LV {level + 1} / {cost} GOLD";
                spellUpgradeTexts[i].color = level >= 3 ? ReinforcementColor : session.Gold >= cost ? GoldColor : MutedColor;
            }
            reinforcementButton.interactable = session.CanCastReinforcements;
            reinforcementPriceText.text = $"{session.ReinforcementCost} MANA";
            reinforcementPriceText.color = session.Mana >= session.ReinforcementCost ? ManaColor : MutedColor;
            reinforcementStatsText.text = $"{session.ReinforcementCount} troops / {session.ReinforcementDuration:0.#}s";
            reinforcementModeText.text = SpellModeText("[R] AIM", reinforcementTargeting,
                session.ReinforcementCooldownRemaining, session.ReinforcementCost);
            reinforcementModeText.color = reinforcementTargeting ? ReinforcementColor : TextColor;
            reinforcementBackground.color = reinforcementTargeting ? new Color(.05f, .24f, .16f, .97f) : PanelColor;
            battlecryButton.interactable = session.CanCastBattlecry;
            battlecryPriceText.text = $"{session.BattlecryCost} MANA";
            battlecryPriceText.color = session.Mana >= session.BattlecryCost ? ManaColor : MutedColor;
            battlecryStatsText.text = $"{session.BattlecryDuration:0.#}s / DMG+SPD+RANGE";
            battlecryModeText.text = session.BattlecryRemaining > 0f ? $"ACTIVE / {session.BattlecryRemaining:0.#}s" :
                SpellModeText("[F] CAST / GLOBAL", false, session.BattlecryCooldownRemaining, session.BattlecryCost);
            battlecryModeText.color = session.BattlecryRemaining > 0f ? BattlecryColor : TextColor;
            battlecryBackground.color = session.BattlecryRemaining > 0f ? new Color(.29f, .17f, .05f, .97f) : PanelColor;
        }

        private string SpellModeText(string ready, bool targeting, float cooldown, int manaCost)
        {
            if (IsFinished()) return "ASSAULT ENDED";
            if (session.IsChoosingAugment) return "CHOOSE AUGMENT";
            if (session.IsPaused) return "PAUSED";
            if (targeting) return "CLICK MAP / RMB CANCEL";
            if (cooldown > 0f) return $"READY IN {Mathf.CeilToInt(cooldown)}s";
            return session.Mana < manaCost ? $"NEED {manaCost - session.Mana} MANA" : ready;
        }

        private bool TryGetGroundPoint(Vector2 screenPoint, out Vector3 point)
        {
            point = default;
            if (gameCamera == null || session.map == null || !gameCamera.pixelRect.Contains(screenPoint))
                return false;
            float floor = session.map.transform.position.y + 0.42f;
            var ground = new Plane(Vector3.up, new Vector3(0f, floor, 0f));
            Ray ray = gameCamera.ScreenPointToRay(screenPoint);
            if (!ground.Raycast(ray, out float distance) || distance <= 0f)
                return false;
            point = ray.GetPoint(distance);
            return session.IsSpellPointValid(point);
        }

        private void CreateRangeCircle()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return;
            rangeMaterial = new Material(shader) { name = "Tower Range Preview (Runtime)" };
            rangeMaterial.SetColor("_BaseColor", new Color(0.24f, 0.94f, 0.96f, 0.78f));
            rangeMaterial.SetFloat("_Surface", 1f);
            rangeMaterial.SetFloat("_Blend", 0f);
            rangeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            rangeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            rangeMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            rangeMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            rangeMaterial.SetFloat("_ZWrite", 0f);
            rangeMaterial.SetFloat("_Cull", (float)CullMode.Off);
            rangeMaterial.SetOverrideTag("RenderType", "Transparent");
            rangeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            rangeMaterial.renderQueue = (int)RenderQueue.Transparent;
            var circleObject = new GameObject("Selected Tower Range");
            circleObject.transform.SetParent(transform, false);
            circleObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            rangeCircle = circleObject.AddComponent<LineRenderer>();
            rangeCircle.sharedMaterial = rangeMaterial;
            rangeCircle.useWorldSpace = true;
            rangeCircle.loop = true;
            rangeCircle.positionCount = rangePoints.Length;
            rangeCircle.widthMultiplier = 0.045f;
            rangeCircle.alignment = LineAlignment.TransformZ;
            rangeCircle.numCornerVertices = 2;
            rangeCircle.shadowCastingMode = ShadowCastingMode.Off;
            rangeCircle.receiveShadows = false;
            rangeCircle.enabled = false;
        }

        private void RefreshRangeCircle(bool visible)
        {
            if (rangeCircle == null)
                return;
            visible &= session != null && !session.IsMenuOpen;
            rangeCircle.enabled = visible;
            if (!visible)
                return;
            selectedTower = session.GetTower(selectedSlot);
            int index = Mathf.Clamp((int)selectedKind, 0, AvailableTowerCount - 1);
            TowerAgent preview = session.towerPrefabs != null && index < session.towerPrefabs.Length
                ? session.towerPrefabs[index] : null;
            float radius = selectedTower != null ? selectedTower.EffectiveRange : preview != null
                ? preview.range + session.Augments.RangeBonus : selectedSlot.previewRadius;
            Vector3 center = selectedSlot.transform.position;
            center.y += 0.10f;
            DrawRangeCircle(center, radius, new Color(0.24f, 0.94f, 0.96f, 0.78f));
        }

        private void DrawRangeCircle(Vector3 center, float radius, Color tint)
        {
            if (rangeCircle == null)
                return;
            if (session != null && session.IsMenuOpen)
            {
                rangeCircle.enabled = false;
                return;
            }
            rangeCircle.enabled = true;
            rangeMaterial.SetColor("_BaseColor", tint);
            if (center == previousRangeCenter && Mathf.Approximately(radius, previousRangeRadius))
                return;
            previousRangeCenter = center;
            previousRangeRadius = radius;
            for (int i = 0; i < rangePoints.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / rangePoints.Length;
                rangePoints[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }
            rangeCircle.SetPositions(rangePoints);
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventObject = new GameObject("Gameplay Event System", typeof(EventSystem));
                eventObject.transform.SetParent(transform, false);
                eventSystem = eventObject.GetComponent<EventSystem>();
            }
            foreach (StandaloneInputModule legacyModule in eventSystem.GetComponents<StandaloneInputModule>())
                legacyModule.enabled = false;
            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            module.enabled = true;
        }

        private void CreateInterface()
        {
            var canvasObject = new GameObject("Gameplay HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            EnsureLocalization();
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Keep the arsenal and inspector apart even on a wide game view.
            scaler.matchWidthOrHeight = AvailableTowerCount > 3 ? 0f : 0.5f;
            if (HasAdvancedSpells)
                scaler.matchWidthOrHeight = Screen.width / (float)Mathf.Max(1, Screen.height) >= 1.6f ? 1f : 0f;
            CreateTopBar();
            CreateProgressBar();
            CreateEnemyLegend();
            if (HasAdvancedSpells)
                CreateAdvancedSpellCards();
            else
            {
                CreateFireballCard();
                CreateFrostCard();
            }
            CreateTowerCards();
            CreateSelectionPanel();
            if (HasAdvancedSpells)
                CreateBossDisplay();
            CreateResultPanel();
            CreateSelectedAugments();
            CreateAugmentSelection();
        }

        private void CreateTopBar()
        {
            RectTransform top = Panel("Resources and Controls", canvas.transform, PanelColor);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = Vector2.one;
            top.offsetMin = new Vector2(16f, -70f);
            top.offsetMax = new Vector2(-16f, -12f);
            titleText = Label(top, "FOREST OUTPOST", new Vector2(18f, -13f), new Vector2(270f, 32f), 22, TextColor);
            ResourceChip(top, 298f, 136f, GoldColor);
            Icon(top, HudIcon.Symbol.Coin, new Vector2(308f, -15f), new Vector2(28f, 28f), GoldColor);
            goldText = Label(top, "180", new Vector2(344f, -12f), new Vector2(85f, 34f), 26, GoldColor);
            ResourceChip(top, 434f, 118f, HealthColor);
            Icon(top, HudIcon.Symbol.Heart, new Vector2(442f, -15f), new Vector2(28f, 28f), HealthColor);
            healthText = Label(top, "20", new Vector2(478f, -12f), new Vector2(64f, 34f), 26, TextColor);
            shieldText = Label(top, "", new Vector2(478f, -34f), new Vector2(77f, 18f), 12, SilverColor);
            ResourceChip(top, 552f, 136f, ManaColor);
            Icon(top, HudIcon.Symbol.Mana, new Vector2(560f, -15f), new Vector2(28f, 28f), ManaColor);
            manaText = Label(top, "0", new Vector2(596f, -12f), new Vector2(82f, 34f), 26, ManaColor);
            ResourceChip(top, 692f, 187f, UiPolish.Teal);
            Icon(top, HudIcon.Symbol.Enemy, new Vector2(700f, -15f), new Vector2(28f, 28f), MutedColor);
            enemiesText = Label(top, "0 ALIVE", new Vector2(736f, -12f), new Vector2(134f, 34f), 22, TextColor);
            speedButton = Button(top, "1x", new Vector2(-124f, -9f), new Vector2(74f, 40f), out speedText);
            AnchorTopRight(speedButton.GetComponent<RectTransform>());
            speedButton.onClick.AddListener(() => { session?.ToggleSpeed(); Refresh(); });
            pauseButton = Button(top, "PAUSE", new Vector2(-14f, -9f), new Vector2(100f, 40f), out pauseText);
            AnchorTopRight(pauseButton.GetComponent<RectTransform>());
            pauseButton.onClick.AddListener(() => { session?.TogglePause(); Refresh(); });
        }

        private void CreateProgressBar()
        {
            RectTransform progress = Panel("Assault Progress", canvas.transform, PanelColor);
            progress.anchorMin = new Vector2(0f, 1f);
            progress.anchorMax = Vector2.one;
            progress.offsetMin = new Vector2(16f, -118f);
            progress.offsetMax = new Vector2(-16f, -76f);
            waveText = Label(progress, "WAVE 0 / 5", new Vector2(18f, -6f), new Vector2(174f, 30f), 21, TextColor);
            progressTrack = Panel("Progress Track", progress, new Color(0.16f, 0.21f, 0.29f));
            progressTrack.anchorMin = new Vector2(0f, 0.5f);
            progressTrack.anchorMax = new Vector2(1f, 0.5f);
            progressTrack.offsetMin = new Vector2(194f, -7f);
            progressTrack.offsetMax = new Vector2(-270f, 7f);
            progressTrack.GetComponent<Image>().raycastTarget = false;
            RectTransform fill = Rect("Progress Fill", progressTrack);
            Stretch(fill);
            progressFill = fill.gameObject.AddComponent<Image>();
            progressFill.color = new Color(0.23f, 0.82f, 0.66f);
            progressFill.raycastTarget = false;
            UiPolish.Skin(progressFill, new Color(.64f, 1f, .79f), 3f);
            countdownText = Label(progress, "ENEMIES APPROACHING", new Vector2(-18f, -6f), new Vector2(244f, 30f), 18, MutedColor);
            AnchorTopRight(countdownText.rectTransform);
            int count = session != null ? Mathf.Max(1, session.WaveCount) : 5;
            for (int i = 0; i < count; i++)
            {
                HudIcon marker = Icon(progressTrack, HudIcon.Symbol.Flag, Vector2.zero, new Vector2(16f, 20f), MutedColor);
                RectTransform rect = marker.rectTransform;
                float fraction = session != null && session.waves != null && i < session.waves.Length && session.waves[i] != null
                    ? Mathf.Clamp01(session.waves[i].startTime / Mathf.Max(1f, session.AssaultDuration))
                    : (i + 1f) / count;
                rect.anchorMin = rect.anchorMax = new Vector2(fraction, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                waveMarkers.Add(marker);
            }
        }

        private void CreateEnemyLegend()
        {
            RectTransform legend = Panel("Enemy Types", canvas.transform, PanelColor);
            int count = session != null && session.enemyProfiles != null ? Mathf.Clamp(session.enemyProfiles.Length, 1, 9) : 3;
            bool compact = count > 3;
            float cellWidth = compact ? 102f : 150f;
            int columns = Mathf.Min(count, 6);
            float cellHeight = count > 6 ? 30f : 43f;
            PlaceTopLeft(legend, new Vector2(16f, -124f), new Vector2(columns * cellWidth + 4f, Mathf.CeilToInt(count / 6f) * cellHeight));
            for (int i = 0; i < count; i++)
            {
                int column = i % 6;
                float top = -(i / 6) * cellHeight;
                int kind = session != null && session.enemyProfiles != null && i < session.enemyProfiles.Length
                    ? Mathf.Clamp((int)session.enemyProfiles[i].kind, 0, EnemyNames.Length - 1) : i;
                Sprite sprite = enemyIcons != null && kind < enemyIcons.Length ? enemyIcons[kind] : null;
                if (sprite != null)
                    SpriteImage(legend, sprite, new Vector2((compact ? 3f : 10f) + column * cellWidth, top - 3f),
                        new Vector2(compact ? 34f : 40f, count > 6 ? 25f : 38f));
                else
                    Icon(legend, kind >= 7 ? HudIcon.Symbol.Boss : kind == 5 ? HudIcon.Symbol.Flying : HudIcon.Symbol.Enemy,
                        new Vector2((compact ? 6f : 15f) + column * cellWidth, top - (count > 6 ? 4f : 10f)),
                        new Vector2(count > 6 ? 22f : 25f, count > 6 ? 22f : 25f), kind >= 7 ? HealthColor : MutedColor);
                Label(legend, EnemyNames[kind], new Vector2((compact ? 38f : 54f) + column * cellWidth, top - (count > 6 ? 0f : 6f)),
                    new Vector2(compact ? 63f : 95f, 30f), compact ? 14 : 18, TextColor);
            }
        }

        private void CreateTowerCards()
        {
            RectTransform tray = Panel("Tower Arsenal", canvas.transform, PanelColor);
            bool compact = AvailableTowerCount > 3;
            bool fiveTowers = AvailableTowerCount > 4;
            float cardWidth = fiveTowers ? 142f : compact ? 184f : 196f;
            float stride = cardWidth + 8f;
            AnchorBottomLeft(tray, new Vector2(AvailableTowerCount * stride + 8f, 146f), 16f, 16f);
            for (int i = 0; i < towerButtons.Length; i++)
            {
                int index = i;
                Button card = Button(tray, "", new Vector2(8f + i * stride, -8f), new Vector2(cardWidth, 130f), out TextMeshProUGUI unused);
                Destroy(unused.gameObject);
                card.gameObject.name = TowerNames[i] + " Tower Card";
                card.onClick.AddListener(() => SelectTowerKind(index));
                towerButtons[i] = card;
                towerCardBackgrounds[i] = card.GetComponent<Image>();
                UiPolish.SetAccent(towerCardBackgrounds[i], TowerColors[i]);
                UiPolish.Feedback(card, 1.015f);
                RectTransform parent = card.GetComponent<RectTransform>();
                RectTransform pedestal = Panel("Tower Icon Pedestal", parent, new Color(.08f, .15f, .19f, .9f));
                PlaceTopLeft(pedestal, new Vector2(6f, -28f), new Vector2(fiveTowers ? 46f : compact ? 61f : 70f, 56f));
                pedestal.GetComponent<Image>().raycastTarget = false;
                UiPolish.SetAccent(pedestal.GetComponent<Image>(), TowerColors[i]);
                RectTransform priceTray = Panel("Tower Price Inset", parent, new Color(.023f, .054f, .073f));
                PlaceTopLeft(priceTray, new Vector2(7f, -84f), new Vector2(cardWidth - 14f, 34f));
                priceTray.GetComponent<Image>().raycastTarget = false;
                Sprite sprite = towerIcons != null && i < towerIcons.Length ? towerIcons[i] : null;
                if (sprite != null)
                    SpriteImage(parent, sprite, new Vector2(7f, -20f), new Vector2(fiveTowers ? 47f : compact ? 60f : 70f, 80f));
                else
                    Icon(parent, TowerSymbols[i], new Vector2(8f, -34f), new Vector2(fiveTowers ? 42f : 52f, fiveTowers ? 42f : 52f), TowerColors[i]);
                float textLeft = fiveTowers ? 56f : compact ? 71f : 80f;
                Label(parent, TowerNames[i], new Vector2(textLeft, -24f), new Vector2(cardWidth - textLeft - 5f, 26f), fiveTowers ? 14 : compact ? 17 : 18, TextColor);
                Label(parent, TowerRoles[i], new Vector2(textLeft, -53f), new Vector2(cardWidth - textLeft - 5f, 25f), fiveTowers ? 12 : compact ? 16 : 18, MutedColor);
                Icon(parent, HudIcon.Symbol.Coin, new Vector2(15f, -90f), new Vector2(20f, 20f), GoldColor);
                towerPrices[i] = Label(parent, "60", new Vector2(43f, -85f), new Vector2(cardWidth - 54f, 30f), fiveTowers ? 19 : 22, GoldColor);
                TextMeshProUGUI selected = Label(parent, "SELECTED", new Vector2(8f, -2f), new Vector2(cardWidth - 40f, 21f), fiveTowers ? 12 : 14, TowerColors[i]);
                selected.alignment = TextAlignmentOptions.Center;
                towerSelectedLabels[i] = selected.gameObject;
                Keycap(parent, (i + 1).ToString(), new Vector2(cardWidth - 28f, -5f), new Vector2(20f, 18f), TowerColors[i]);
                card.gameObject.SetActive(i < AvailableTowerCount);
            }
        }

        private void CreateFireballCard()
        {
            fireballButton = Button(canvas.transform, "", Vector2.zero, new Vector2(260f, 134f), out TextMeshProUGUI unused);
            Destroy(unused.gameObject);
            fireballButton.gameObject.name = "Fireball Spell Card";
            AnchorBottomRight(fireballButton.GetComponent<RectTransform>(), new Vector2(260f, 134f), 16f, 182f);
            fireballBackground = fireballButton.GetComponent<Image>();
            fireballBackground.color = PanelColor;
            UiPolish.SetAccent(fireballBackground, FireballColor);
            RectTransform card = fireballButton.GetComponent<RectTransform>();
            Icon(card, HudIcon.Symbol.Hexagon, new Vector2(7f, -10f), new Vector2(64f, 64f), new Color(1f, .50f, .25f, .45f));
            Icon(card, HudIcon.Symbol.Fireball, new Vector2(13f, -14f), new Vector2(52f, 58f), FireballColor);
            Label(card, "FIREBALL", new Vector2(76f, -13f), new Vector2(162f, 27f), 23, TextColor);
            Icon(card, HudIcon.Symbol.Mana, new Vector2(78f, -45f), new Vector2(22f, 22f), ManaColor);
            fireballPriceText = Label(card, "12 MANA", new Vector2(107f, -42f), new Vector2(134f, 28f), 20, ManaColor);
            fireballStatsText = Label(card, "3s burn / R 3.2", new Vector2(16f, -75f), new Vector2(222f, 25f), 18, MutedColor);
            fireballModeText = Label(card, "[Q] AIM", new Vector2(16f, -103f), new Vector2(222f, 25f), 18, TextColor);
            fireballButton.onClick.AddListener(() =>
            {
                if (fireballTargeting)
                    CancelFireballTargeting();
                else
                    BeginFireballTargeting();
            });
        }

        private void CreateFrostCard()
        {
            frostButton = Button(canvas.transform, "", Vector2.zero, new Vector2(260f, 134f), out TextMeshProUGUI unused);
            Destroy(unused.gameObject);
            frostButton.gameObject.name = "Frost Nova Spell Card";
            AnchorBottomRight(frostButton.GetComponent<RectTransform>(), new Vector2(260f, 134f), 16f, 326f);
            frostBackground = frostButton.GetComponent<Image>();
            frostBackground.color = PanelColor;
            UiPolish.SetAccent(frostBackground, FrostSpellColor);
            RectTransform card = frostButton.GetComponent<RectTransform>();
            Icon(card, HudIcon.Symbol.Hexagon, new Vector2(7f, -10f), new Vector2(64f, 64f), new Color(.40f, .89f, 1f, .45f));
            Icon(card, HudIcon.Symbol.FrostSpell, new Vector2(13f, -14f), new Vector2(52f, 58f), FrostSpellColor);
            Label(card, "FROST NOVA", new Vector2(76f, -13f), new Vector2(170f, 27f), 22, TextColor);
            Icon(card, HudIcon.Symbol.Mana, new Vector2(78f, -45f), new Vector2(22f, 22f), ManaColor);
            frostPriceText = Label(card, "18 MANA", new Vector2(107f, -42f), new Vector2(134f, 28f), 20, ManaColor);
            frostStatsText = Label(card, "3.5s freeze / R 3.8", new Vector2(16f, -75f), new Vector2(230f, 25f), 18, MutedColor);
            frostModeText = Label(card, "[E] AIM", new Vector2(16f, -103f), new Vector2(230f, 25f), 18, TextColor);
            frostButton.onClick.AddListener(() =>
            {
                if (frostTargeting)
                    CancelFireballTargeting();
                else
                    BeginFrostTargeting();
            });
            frostButton.gameObject.SetActive(session != null && session.FrostSpellUnlocked);
        }

        private void CreateAdvancedSpellCards()
        {
            string[] names = { "FIREBALL", "FROST NOVA", "REINFORCEMENTS", "BATTLECRY" };
            HudIcon.Symbol[] symbols = { HudIcon.Symbol.Fireball, HudIcon.Symbol.FrostSpell,
                HudIcon.Symbol.Reinforcements, HudIcon.Symbol.Battlecry };
            Color[] colors = { FireballColor, FrostSpellColor, ReinforcementColor, BattlecryColor };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                SpellKind spell = (SpellKind)i;
                RectTransform panel = Panel(names[i] + " Spell Panel", canvas.transform, PanelColor);
                bool twoSpellLayout = session != null && session.levelIndex == 2;
                AnchorBottomRight(panel, new Vector2(214f, 134f), twoSpellLayout ? 16f : i % 2 == 0 ? 240f : 16f,
                    182f + (twoSpellLayout ? i : i / 2) * 144f);
                // Casting and upgrading use separate buttons. The upgrade click never reaches the cast button.
                Button cast = Button(panel, "", Vector2.zero, new Vector2(214f, 104f), out TextMeshProUGUI unused);
                Destroy(unused.gameObject);
                cast.gameObject.name = names[i] + " Cast Button";
                Image background = cast.GetComponent<Image>();
                background.color = PanelColor;
                UiPolish.SetAccent(background, colors[i]);
                UiPolish.SetAccent(panel.GetComponent<Image>(), colors[i]);
                RectTransform content = cast.GetComponent<RectTransform>();
                Color crestTint = colors[i];
                crestTint.a = .44f;
                Icon(content, HudIcon.Symbol.Hexagon, new Vector2(4f, -5f), new Vector2(42f, 44f), crestTint);
                Icon(content, symbols[i], new Vector2(9f, -10f), new Vector2(32f, 35f), colors[i]);
                spellTitleTexts[i] = Label(content, names[i], new Vector2(49f, -6f), new Vector2(159f, 25f), 17, TextColor);
                WrapText(spellTitleTexts[i], 14f, 17f);
                TextMeshProUGUI price = Label(content, "", new Vector2(49f, -31f), new Vector2(159f, 24f), 17, ManaColor);
                TextMeshProUGUI stats = Label(content, "", new Vector2(10f, -56f), new Vector2(194f, 24f), 15, MutedColor);
                TextMeshProUGUI mode = Label(content, "", new Vector2(10f, -80f), new Vector2(194f, 22f), 14, TextColor);
                Button upgrade = Button(panel, "", new Vector2(4f, -106f), new Vector2(206f, 24f), out TextMeshProUGUI upgradeText);
                upgrade.gameObject.name = names[i] + " Upgrade Button";
                UiSound.Attach(upgrade).SuppressClick = true;
                upgradeText.fontSize = 14;
                upgrade.GetComponent<Image>().color = new Color(.25f, .22f, .12f);
                UiPolish.SetAccent(upgrade.GetComponent<Image>(), GoldColor);
                upgrade.onClick.AddListener(() => UpgradeSpell(spell));
                spellUpgradeButtons[i] = upgrade;
                spellUpgradeTexts[i] = upgradeText;
                EventTrigger hover = panel.gameObject.AddComponent<EventTrigger>();
                AddPointerEvent(hover, EventTriggerType.PointerEnter, () => ShowSpellTooltip((SpellKind)index));
                AddPointerEvent(hover, EventTriggerType.PointerExit, HideAugmentTooltip);
                if (i == 0)
                {
                    fireballButton = cast;
                    fireballBackground = background;
                    fireballPriceText = price;
                    fireballStatsText = stats;
                    fireballModeText = mode;
                    cast.onClick.AddListener(() => { if (fireballTargeting) CancelSpellTargeting(); else BeginFireballTargeting(); });
                }
                else if (i == 1)
                {
                    frostButton = cast;
                    frostBackground = background;
                    frostPriceText = price;
                    frostStatsText = stats;
                    frostModeText = mode;
                    cast.onClick.AddListener(() => { if (frostTargeting) CancelSpellTargeting(); else BeginFrostTargeting(); });
                }
                else if (i == 2)
                {
                    reinforcementButton = cast;
                    reinforcementBackground = background;
                    reinforcementPriceText = price;
                    reinforcementStatsText = stats;
                    reinforcementModeText = mode;
                    cast.onClick.AddListener(() => { if (reinforcementTargeting) CancelSpellTargeting(); else BeginReinforcementTargeting(); });
                }
                else
                {
                    battlecryButton = cast;
                    UiSound.Attach(cast).SuppressClick = true;
                    battlecryBackground = background;
                    battlecryPriceText = price;
                    battlecryStatsText = stats;
                    battlecryModeText = mode;
                    cast.onClick.AddListener(CastBattlecry);
                }
                if (twoSpellLayout && i >= 2) panel.gameObject.SetActive(false);
            }
        }

        private void UpgradeSpell(SpellKind spell)
        {
            if (session == null)
                return;
            blockWorldInputUntilFrame = Time.frameCount + 1;
            bool upgraded = session.TryUpgradeSpell(spell);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            Refresh();
            if (upgraded && augmentTooltip != null && augmentTooltip.activeSelf)
                ShowSpellTooltip(spell);
        }

        private void ShowSpellTooltip(SpellKind spell)
        {
            if (session == null || !HasAdvancedSpells || session.IsChoosingAugment || IsFinished() || augmentTooltip == null)
                return;
            augmentTooltipTitle.text = $"{spell.ToString().ToUpperInvariant()} / LV {session.GetSpellLevel(spell)}";
            augmentTooltipTitle.color = GoldColor;
            augmentTooltipDescription.text = session.GetSpellSummary(spell);
            augmentTooltip.SetActive(true);
            augmentTooltip.transform.SetAsLastSibling();
        }

        private void CreateBossDisplay()
        {
            RectTransform panel = Panel("Boss Health", canvas.transform, new Color(.16f, .045f, .08f, .96f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -174f);
            panel.sizeDelta = new Vector2(480f, 52f);
            panel.GetComponent<Image>().raycastTarget = false;
            UiPolish.SetAccent(panel.GetComponent<Image>(), HealthColor, true);
            UiPolish.Entrance(panel.gameObject);
            bossPanel = panel.gameObject;
            Icon(panel, HudIcon.Symbol.Boss, new Vector2(11f, -7f), new Vector2(24f, 25f), HealthColor);
            bossText = Label(panel, "", new Vector2(43f, -3f), new Vector2(424f, 29f), 18, TextColor);
            RectTransform track = Panel("Boss Health Track", panel, new Color(.30f, .12f, .16f));
            PlaceTopLeft(track, new Vector2(13f, -34f), new Vector2(454f, 9f));
            track.GetComponent<Image>().raycastTarget = false;
            RectTransform fill = Panel("Boss Health Fill", track, HealthColor);
            Stretch(fill);
            bossHealthFill = fill.GetComponent<Image>();
            bossHealthFill.raycastTarget = false;
            bossPanel.SetActive(false);
        }

        private void RefreshBossDisplay()
        {
            if (bossPanel == null)
                return;
            EnemyAgent boss = null;
            foreach (EnemyAgent candidate in session.ActiveEnemies)
                if (candidate != null && candidate.IsAlive && candidate.IsBoss)
                {
                    boss = candidate;
                    break;
                }
            bool visible = boss != null && !IsFinished() && !session.IsChoosingAugment;
            bossPanel.SetActive(visible);
            if (!visible)
                return;
            bossText.text = $"BOSS{(boss.IsEnraged ? " / ENRAGED" : "")}    {boss.CurrentHealth} / {boss.MaxHealth} HP";
            bossHealthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(boss.CurrentHealth / (float)Mathf.Max(1, boss.MaxHealth)), 1f);
            bossHealthFill.color = boss.IsEnraged ? new Color(1f, .54f, .13f) : HealthColor;
        }

        private void CreateSelectionPanel()
        {
            RectTransform hint = Panel("Build Hint", canvas.transform, PanelColor);
            AnchorBottomRight(hint, new Vector2(438f, 88f), 16f, 16f);
            hintPanel = hint.gameObject;
            hintText = Label(hint, "Pick a tower and pad, then BUILD.", new Vector2(18f, -12f), new Vector2(402f, 28f), 21, TextColor);
            statusText = Label(hint, "Build and upgrade during the assault.", new Vector2(18f, -44f), new Vector2(402f, 29f), 18, MutedColor);
            RectTransform selection = Panel("Selected Pad", canvas.transform, PanelColor);
            AnchorBottomRight(selection, new Vector2(438f, 146f), 16f, 16f);
            selectionPanel = selection.gameObject;
            slotTitle = Label(selection, "BUILD PAD", new Vector2(18f, -10f), new Vector2(344f, 28f), 22, TextColor);
            slotDescription = Label(selection, "", new Vector2(18f, -41f), new Vector2(402f, 38f), 18, MutedColor);
            slotDescription.alignment = TextAlignmentOptions.TopLeft;
            WrapText(slotDescription, 14f, 18f);
            buildButton = Button(selection, "BUILD", new Vector2(18f, -88f), new Vector2(402f, 44f), out buildText);
            UiSound.Attach(buildButton).SuppressClick = true;
            buildButton.onClick.AddListener(() =>
            {
                if (session == null || session.IsChoosingAugment || selectedSlot == null)
                    return;
                if (session.GetTower(selectedSlot) != null)
                    session.TryUpgradeTower(selectedSlot);
                else
                    session.TryBuildTower(selectedSlot, selectedKind);
                Refresh();
            });
            Button close = Button(selection, "X", new Vector2(-10f, -8f), new Vector2(32f, 28f), out TextMeshProUGUI closeLabel);
            AnchorTopRight(close.GetComponent<RectTransform>());
            close.GetComponent<Image>().color = new Color(0.15f, 0.20f, 0.27f);
            closeLabel.fontSize = 18;
            close.onClick.AddListener(() => SelectSlot(null));
        }

        private void CreateResultPanel()
        {
            RectTransform overlay = Panel("Result Overlay", canvas.transform, new Color(0.015f, 0.025f, 0.045f, 0.82f));
            Stretch(overlay);
            resultPanel = overlay.gameObject;
            RectTransform card = Panel("Result Card", overlay, PanelColor);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(520f, 330f);
            resultTitle = Label(card, "VICTORY", new Vector2(24f, -28f), new Vector2(472f, 48f), 38, TextColor);
            resultTitle.alignment = TextAlignmentOptions.Center;
            resultDescription = Label(card, "", new Vector2(24f, -92f), new Vector2(472f, 70f), 22, MutedColor);
            resultDescription.alignment = TextAlignmentOptions.Center;
            Button restart = Button(card, "PLAY AGAIN", new Vector2(130f, -180f), new Vector2(260f, 50f), out _);
            restart.onClick.AddListener(() => { SelectSlot(null); session?.RestartLevel(); });
            nextLevelButton = Button(card, "NEXT LEVEL / CANYON", new Vector2(100f, -242f), new Vector2(320f, 50f), out nextLevelText);
            nextLevelButton.onClick.AddListener(() => session?.LoadNextLevel());
            nextLevelButton.gameObject.SetActive(false);
            firstLevelButton = Button(card, "BACK TO LEVEL 1", new Vector2(100f, -242f), new Vector2(320f, 50f), out _);
            firstLevelButton.onClick.AddListener(() => session?.LoadFirstLevel());
            firstLevelButton.gameObject.SetActive(false);
        }

        private void CreateSelectedAugments()
        {
            RectTransform collection = Panel("Selected Augments", canvas.transform, PanelColor);
            collection.anchorMin = collection.anchorMax = collection.pivot = Vector2.one;
            collection.anchoredPosition = new Vector2(-16f, -124f);
            collection.sizeDelta = new Vector2(610f, 43f);
            int count = AugmentChoiceCount;
            float stride = count > 2 ? 202f : 302f;
            float width = stride - 4f;
            for (int i = 0; i < selectedAugmentNames.Length; i++)
            {
                int index = i;
                RectTransform chip = Panel("Selected Augment " + (i + 1), collection, new Color(0.12f, 0.17f, 0.23f));
                PlaceTopLeft(chip, new Vector2(5f + i * stride, -5f), new Vector2(width, 33f));
                selectedAugmentBorders[i] = chip.GetComponent<Image>();
                selectedAugmentIcons[i] = Icon(chip, HudIcon.Symbol.Upgrade, new Vector2(9f, -6f), new Vector2(21f, 21f), MutedColor);
                selectedAugmentNames[i] = Label(chip, "", new Vector2(39f, -4f), new Vector2(width - 47f, 25f), count > 2 ? 15 : 17, MutedColor);
                EventTrigger events = chip.gameObject.AddComponent<EventTrigger>();
                AddPointerEvent(events, EventTriggerType.PointerEnter, () => ShowAugmentTooltip(index));
                AddPointerEvent(events, EventTriggerType.PointerExit, HideAugmentTooltip);
                chip.gameObject.SetActive(i < count);
            }
            RectTransform tooltip = Panel("Augment Details", canvas.transform, PanelColor);
            tooltip.anchorMin = tooltip.anchorMax = tooltip.pivot = Vector2.one;
            tooltip.anchoredPosition = new Vector2(-16f, -174f);
            tooltip.sizeDelta = new Vector2(420f, 148f);
            tooltip.GetComponent<Image>().raycastTarget = false;
            augmentTooltip = tooltip.gameObject;
            augmentTooltipTitle = Label(tooltip, "", new Vector2(16f, -12f), new Vector2(388f, 30f), 21, SilverColor);
            augmentTooltipDescription = Label(tooltip, "", new Vector2(16f, -49f), new Vector2(388f, 88f), 18, TextColor);
            WrapText(augmentTooltipDescription, 17f, 18f);
            augmentTooltip.SetActive(false);
        }

        private void CreateAugmentSelection()
        {
            RectTransform overlay = Panel("Augment Choice Overlay", canvas.transform, new Color(0.009f, 0.018f, 0.035f, 0.92f));
            Stretch(overlay);
            augmentPanel = overlay.gameObject;
            // The full-screen graphic stays raycastable: no click can reach a pad behind the modal.
            RectTransform frame = Panel("Augment Choice Frame", overlay, new Color(0.035f, 0.055f, 0.083f));
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1088f, 536f);
            UiPolish.SetAccent(frame.GetComponent<Image>(), UiPolish.Trim);
            UiPolish.Entrance(frame.gameObject);
            Icon(frame, HudIcon.Symbol.Hexagon, new Vector2(24f, -23f), new Vector2(58f, 58f), new Color(.64f, .86f, .82f, .40f));
            augmentTitle = Label(frame, "CHOOSE YOUR AUGMENT", new Vector2(28f, -23f), new Vector2(1032f, 42f), 34, TextColor);
            augmentTitle.alignment = TextAlignmentOptions.Center;
            augmentSubtitle = Label(frame, "OPENING CHOICE", new Vector2(28f, -69f), new Vector2(1032f, 28f), 19, SilverColor);
            augmentSubtitle.alignment = TextAlignmentOptions.Center;
            for (int i = 0; i < augmentButtons.Length; i++)
            {
                int index = i;
                bool canRefresh = session != null && session.AugmentRefreshUnlocked;
                RectTransform border = Panel("Augment Card Border " + (i + 1), frame, SilverColor);
                PlaceTopLeft(border, new Vector2(26f + i * 352f, -116f), new Vector2(332f, 380f));
                border.GetComponent<Image>().raycastTarget = false;
                augmentCardBorders[i] = border.GetComponent<Image>();
                Button choice = Button(border, "", new Vector2(2f, -2f), new Vector2(328f, canRefresh ? 332f : 376f), out TextMeshProUGUI unused);
                Destroy(unused.gameObject);
                choice.gameObject.name = "Choose Augment " + (i + 1);
                choice.GetComponent<Image>().color = new Color(0.085f, 0.12f, 0.17f);
                UiPolish.Entrance(border.gameObject, i * .045f);
                UiPolish.Feedback(choice, 1.008f);
                ColorBlock colors = choice.colors;
                colors.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
                colors.fadeDuration = 0.12f;
                choice.colors = colors;
                choice.onClick.AddListener(() => ChooseAugment(index));
                UiSound.Attach(choice).SuppressClick = true;
                augmentButtons[i] = choice;
                RectTransform content = choice.GetComponent<RectTransform>();
                augmentTierLabels[i] = Label(content, "SILVER AUGMENT", new Vector2(18f, -12f), new Vector2(292f, 24f), 15, SilverColor);
                augmentTierLabels[i].alignment = TextAlignmentOptions.Center;
                augmentMedallions[i] = Icon(content, HudIcon.Symbol.Hexagon,
                    new Vector2(canRefresh ? 132f : 124f, canRefresh ? -34f : -38f),
                    new Vector2(canRefresh ? 64f : 80f, canRefresh ? 64f : 80f), SilverColor);
                augmentIcons[i] = Icon(content, HudIcon.Symbol.Upgrade,
                    new Vector2(canRefresh ? 142f : 134f, canRefresh ? -44f : -48f),
                    new Vector2(canRefresh ? 44f : 60f, canRefresh ? 44f : 60f), SilverColor);
                augmentNames[i] = Label(content, "", new Vector2(18f, canRefresh ? -96f : -119f),
                    new Vector2(292f, canRefresh ? 49f : 59f), canRefresh ? 24 : 26, TextColor);
                augmentNames[i].gameObject.name = "Augment Name";
                augmentNames[i].alignment = TextAlignmentOptions.Center;
                WrapText(augmentNames[i], canRefresh ? 21f : 22f, canRefresh ? 24f : 26f);
                augmentDescriptions[i] = Label(content, "", new Vector2(22f, canRefresh ? -153f : -188f),
                    new Vector2(284f, canRefresh ? 105f : 102f), 20, TextColor);
                augmentDescriptions[i].gameObject.name = "Augment Effect";
                augmentDescriptions[i].alignment = TextAlignmentOptions.TopLeft;
                WrapText(augmentDescriptions[i], 16f, 20f);
                augmentSynergyHints[i] = Label(content, "", new Vector2(22f, canRefresh ? -262f : -293f),
                    new Vector2(284f, 31f), 14, UiPolish.Teal);
                augmentSynergyHints[i].gameObject.name = "Augment Synergy Hint";
                WrapText(augmentSynergyHints[i], 12f, 14f);
                RectTransform footer = Panel("Choose Footer", content, new Color(0.12f, 0.19f, 0.26f));
                PlaceTopLeft(footer, new Vector2(10f, canRefresh ? -298f : -338f), new Vector2(308f, 28f));
                footer.GetComponent<Image>().raycastTarget = false;
                TextMeshProUGUI action = Label(footer, "CHOOSE THIS AUGMENT", Vector2.zero, new Vector2(308f, 28f), 16, TextColor);
                action.alignment = TextAlignmentOptions.Center;
                augmentRefreshButtons[i] = Button(border, "REFRESH / 1 LEFT", new Vector2(14f, -342f),
                    new Vector2(304f, 28f), out augmentRefreshTexts[i]);
                augmentRefreshButtons[i].gameObject.name = "Refresh Augment " + (i + 1);
                UiSound.Attach(augmentRefreshButtons[i]).SuppressClick = true;
                augmentRefreshTexts[i].fontSize = 16;
                Icon(augmentRefreshButtons[i].transform, HudIcon.Symbol.Refresh, new Vector2(12f, -6f), new Vector2(16f, 16f), UiPolish.Teal);
                augmentRefreshButtons[i].onClick.AddListener(() => RefreshAugment(index));
                augmentRefreshButtons[i].gameObject.SetActive(canRefresh);
                augmentRainbowFrames[i] = CreatePrismaticFrame(border);
                augmentRainbowFrames[i].SetActive(false);
            }
            string help = session != null && session.AugmentRefreshUnlocked ?
                "Choose one card. Each slot can be refreshed once per choice. Hover equipped cards to review their effects." :
                "Choose one card. The effect lasts for this battle. Hover your equipped cards to review them.";
            TextMeshProUGUI instruction = Label(frame, help,
                new Vector2(28f, -508f), new Vector2(1032f, 22f), 16, MutedColor);
            instruction.alignment = TextAlignmentOptions.Center;
            augmentPanel.SetActive(false);
        }

        private GameObject CreatePrismaticFrame(RectTransform parent)
        {
            RectTransform frame = Rect("Prismatic Spectrum", parent);
            Stretch(frame);
            Color[] spectrum = { new Color(1f, .46f, .62f), new Color(1f, .79f, .32f),
                new Color(.48f, 1f, .74f), new Color(.37f, .84f, 1f), new Color(.71f, .57f, 1f), new Color(1f, .55f, .91f) };
            for (int i = 0; i < spectrum.Length; i++)
            {
                float left = i / (float)spectrum.Length;
                float right = (i + 1f) / spectrum.Length;
                RectTransform top = Panel("Spectrum Top", frame, spectrum[i]);
                top.anchorMin = new Vector2(left, 1f);
                top.anchorMax = new Vector2(right, 1f);
                top.offsetMin = new Vector2(0f, -3f);
                top.offsetMax = Vector2.zero;
                top.GetComponent<Image>().raycastTarget = false;
                RectTransform bottom = Panel("Spectrum Bottom", frame, spectrum[spectrum.Length - 1 - i]);
                bottom.anchorMin = new Vector2(left, 0f);
                bottom.anchorMax = new Vector2(right, 0f);
                bottom.offsetMin = Vector2.zero;
                bottom.offsetMax = new Vector2(0f, 3f);
                bottom.GetComponent<Image>().raycastTarget = false;
                RectTransform side = Panel("Spectrum Left", frame, spectrum[i]);
                side.anchorMin = new Vector2(0f, left);
                side.anchorMax = new Vector2(0f, right);
                side.offsetMin = Vector2.zero;
                side.offsetMax = new Vector2(3f, 0f);
                side.GetComponent<Image>().raycastTarget = false;
                RectTransform opposite = Panel("Spectrum Right", frame, spectrum[spectrum.Length - 1 - i]);
                opposite.anchorMin = new Vector2(1f, left);
                opposite.anchorMax = new Vector2(1f, right);
                opposite.offsetMin = new Vector2(-3f, 0f);
                opposite.offsetMax = Vector2.zero;
                opposite.GetComponent<Image>().raycastTarget = false;
            }
            return frame.gameObject;
        }

        private static void AddPointerEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => callback());
            trigger.triggers.Add(entry);
        }

        private static void WrapText(TextMeshProUGUI text, float minSize, float maxSize)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = minSize;
            text.fontSizeMax = maxSize;
        }

        private void ChooseAugment(int index)
        {
            if (session == null || !session.IsChoosingAugment)
                return;
            // The UI closes during this click. Suppress map input for its release/frame as well.
            blockWorldInputUntilFrame = Time.frameCount + 1;
            HideAugmentTooltip();
            session.TryChooseAugment(index);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            Refresh();
        }

        private void RefreshAugment(int index)
        {
            if (session == null || !session.CanRefreshAugment(index))
                return;
            blockWorldInputUntilFrame = Time.frameCount + 1;
            session.TryRefreshAugment(index);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            Refresh();
        }

        private void ShowAugmentTooltip(int index)
        {
            if (session == null || session.IsChoosingAugment || IsFinished() || index >= session.SelectedAugments.Count)
                return;
            AugmentDefinition definition = session.SelectedAugments[index];
            augmentTooltipTitle.text = definition.Name;
            augmentTooltipTitle.color = AugmentColor(definition.Tier);
            augmentTooltipDescription.text = definition.Description + "\n" + definition.SynergyHint;
            augmentTooltip.SetActive(true);
            augmentTooltip.transform.SetAsLastSibling();
        }

        private void HideAugmentTooltip()
        {
            if (augmentTooltip != null)
                augmentTooltip.SetActive(false);
        }

        private void RefreshAugments()
        {
            bool choosing = session.IsChoosingAugment && !IsFinished();
            augmentPanel.SetActive(choosing);
            if (choosing)
            {
                augmentPanel.transform.SetAsLastSibling();
                HideAugmentTooltip();
                AugmentTier tier = session.AugmentOffers.Count > 0 ? session.AugmentOffers[0].Tier : AugmentTier.Silver;
                augmentTitle.text = session.AugmentChoiceNumber <= 1 ? "CHOOSE YOUR AUGMENT" :
                    tier == AugmentTier.Prismatic ? "UNLEASH YOUR PRISMATIC POWER" : "REINFORCE YOUR STRATEGY";
                string stage = session.AugmentChoiceNumber <= 1 ? "OPENING" : session.AugmentChoiceNumber >= 3 ? "LATE GAME" : "MIDGAME";
                augmentSubtitle.text = $"{stage} CHOICE / {session.AugmentChoiceNumber} OF {AugmentChoiceCount} / " +
                    (session.AugmentChoiceNumber <= 1 ? "BATTLE STARTS AFTER YOUR PICK" : "BATTLE PAUSED WHILE YOU CHOOSE");
                augmentSubtitle.color = AugmentColor(tier);
                for (int i = 0; i < augmentButtons.Length; i++)
                {
                    bool available = i < session.AugmentOffers.Count;
                    augmentButtons[i].interactable = available;
                    augmentCardBorders[i].gameObject.SetActive(available);
                    if (!available)
                        continue;
                    AugmentDefinition definition = session.AugmentOffers[i];
                    Color tint = AugmentColor(definition.Tier);
                    augmentCardBorders[i].color = new Color(tint.r * .18f, tint.g * .20f, tint.b * .24f, .99f);
                    UiPolish.SetAccent(augmentCardBorders[i], tint, true);
                    UiPolish.SetAccent(augmentButtons[i].GetComponent<Image>(), tint);
                    augmentRainbowFrames[i].SetActive(definition.Tier == AugmentTier.Prismatic);
                    augmentTierLabels[i].text = definition.Tier.ToString().ToUpperInvariant() + " AUGMENT";
                    augmentTierLabels[i].color = tint;
                    augmentIcons[i].symbol = GetAugmentSymbol(definition);
                    augmentIcons[i].color = tint;
                    augmentIcons[i].SetVerticesDirty();
                    if (augmentMedallions[i] != null)
                    {
                        augmentMedallions[i].color = tint;
                        augmentMedallions[i].SetVerticesDirty();
                    }
                    augmentNames[i].text = definition.Name;
                    augmentDescriptions[i].text = definition.Description;
                    augmentSynergyHints[i].text = definition.SynergyHint;
                    augmentRefreshButtons[i].gameObject.SetActive(session.AugmentRefreshUnlocked);
                    bool refreshReady = session.CanRefreshAugment(i);
                    augmentRefreshButtons[i].interactable = refreshReady;
                    augmentRefreshTexts[i].text = refreshReady ? "REFRESH / 1 LEFT" : "REFRESH USED";
                    augmentRefreshTexts[i].color = refreshReady ? TextColor : MutedColor;
                }
            }
            else if (IsFinished() || session.SelectedAugments.Count == 0)
                HideAugmentTooltip();
            for (int i = 0; i < selectedAugmentNames.Length; i++)
            {
                selectedAugmentBorders[i].gameObject.SetActive(i < AugmentChoiceCount);
                if (i >= AugmentChoiceCount)
                    continue;
                bool equipped = i < session.SelectedAugments.Count;
                AugmentDefinition definition = equipped ? session.SelectedAugments[i] : null;
                Color tint = equipped ? AugmentColor(definition.Tier) : MutedColor;
                selectedAugmentBorders[i].color = equipped ? new Color(tint.r * 0.14f, tint.g * 0.19f, tint.b * 0.24f, 0.98f) :
                    new Color(0.09f, 0.12f, 0.17f);
                selectedAugmentNames[i].text = equipped ? definition.Name : i == 0 ? "SILVER / OPENING" :
                    i == 1 ? "GOLD / MIDGAME" : "PRISMATIC / LATE";
                selectedAugmentNames[i].color = tint;
                selectedAugmentIcons[i].symbol = equipped ? GetAugmentSymbol(definition) : HudIcon.Symbol.Upgrade;
                selectedAugmentIcons[i].color = tint;
                selectedAugmentIcons[i].SetVerticesDirty();
                UiPolish.SetAccent(selectedAugmentBorders[i], tint, equipped);
            }
        }

        private static Color AugmentColor(AugmentTier tier) => tier == AugmentTier.Gold ? GoldColor :
            tier == AugmentTier.Prismatic ? PrismaticColor : SilverColor;

        private static HudIcon.Symbol GetAugmentSymbol(AugmentDefinition definition)
        {
            return definition.Id switch
            {
                AugmentId.LuckyLoot or AugmentId.ConstructionGrant or AugmentId.FieldWorkshop or AugmentId.EmberLedger or AugmentId.ArcaneInvestment => HudIcon.Symbol.Coin,
                AugmentId.ManaScavenger or AugmentId.StoredMana or AugmentId.FrostBattery or AugmentId.SpellPrimer or AugmentId.ArcaneSupremacy or AugmentId.ArchmageCycle or AugmentId.ManaFurnace => HudIcon.Symbol.Mana,
                AugmentId.LastingFrost or AugmentId.FrostMark or AugmentId.ShatterResonance or AugmentId.ColdFront or AugmentId.GlacialCataclysm or AugmentId.PolarPulse => HudIcon.Symbol.Frost,
                AugmentId.FocusedFlame or AugmentId.ExtendedBurn or AugmentId.FireDividend or AugmentId.BurningMark or AugmentId.ScorchingEdges or AugmentId.CinderHarvest or AugmentId.SteamCircuit or AugmentId.TwinMeteor or AugmentId.ElementalBoil or AugmentId.GrandConvergence => HudIcon.Symbol.Fireball,
                AugmentId.EmergencyShield or AugmentId.FieldMedic or AugmentId.PhoenixPact => HudIcon.Symbol.Heart,
                AugmentId.BlastCalibration or AugmentId.HeavyHunter or AugmentId.PowderKeg or AugmentId.SiegeFormation or AugmentId.Aftershock => HudIcon.Symbol.Cannon,
                AugmentId.LongRange or AugmentId.GunRhythm or AugmentId.Crossfire or AugmentId.PatientAim or AugmentId.FinishingBlow or AugmentId.PiercingRounds => HudIcon.Symbol.Gun,
                AugmentId.VeteranTraining or AugmentId.FastReinforcements or AugmentId.HeroicGuard or AugmentId.GuardianBond or AugmentId.ExplosiveFarewell or AugmentId.EternalLegion or AugmentId.VanguardCommand => HudIcon.Symbol.Barracks,
                AugmentId.SanctuaryRoots or AugmentId.SanctuaryReactor => HudIcon.Symbol.Support,
                AugmentId.WarDrums or AugmentId.Warpath => HudIcon.Symbol.Battlecry,
                _ => HudIcon.Symbol.Upgrade
            };
        }

        private bool IsFinished() => session.Phase == SessionPhase.Victory || session.Phase == SessionPhase.Defeat;

        private void Refresh()
        {
            if (!initialized)
                return;
            if (!HasValidInterface())
            {
                EnsureInitialized();
                return;
            }
            if (session == null)
            {
                RefreshRangeCircle(false);
                titleText.text = "TOWER DEFENSE";
                statusText.text = "Waiting for the level.";
                speedButton.interactable = pauseButton.interactable = false;
                fireballButton.interactable = false;
                frostButton.gameObject.SetActive(false);
                if (reinforcementButton != null) reinforcementButton.gameObject.SetActive(false);
                if (battlecryButton != null) battlecryButton.gameObject.SetActive(false);
                selectionPanel.SetActive(false);
                resultPanel.SetActive(false);
                augmentPanel.SetActive(false);
                HideAugmentTooltip();
                return;
            }
            bool finished = IsFinished();
            bool choosing = session.IsChoosingAugment;
            bool menuOpen = session.IsMenuOpen;
            if (gameplayInteraction != null)
                gameplayInteraction.interactable = gameplayInteraction.blocksRaycasts = !menuOpen;
            if (finished || choosing || session.IsPaused || menuOpen)
            {
                fireballTargeting = false;
                frostTargeting = false;
                reinforcementTargeting = false;
            }
            if (!session.CanCastFireball)
                fireballTargeting = false;
            if (!session.FrostSpellUnlocked || session.Mana < session.FrostSpellCost)
                frostTargeting = false;
            if (!session.CanCastReinforcements)
                reinforcementTargeting = false;
            selectedKind = (TowerKind)Mathf.Clamp((int)selectedKind, 0, AvailableTowerCount - 1);
            if (choosing)
            {
                selectedSlot = null;
                selectedTower = null;
            }
            titleText.text = GetLevelTitle();
            goldText.text = session.IsCheatMode ? "∞" : session.Gold.ToString();
            healthText.text = session.IsCheatMode ? "∞" : session.BaseHealth.ToString();
            healthText.rectTransform.anchoredPosition = new Vector2(478f, session.BaseShield > 0 ? -2f : -12f);
            shieldText.SetText("+{0} SHIELD", session.BaseShield);
            shieldText.gameObject.SetActive(session.BaseShield > 0);
            manaText.text = session.IsCheatMode ? "∞" : session.Mana.ToString();
            enemiesText.SetText("{0} ALIVE", session.ActiveEnemies.Count);
            speedButton.interactable = pauseButton.interactable = !finished && !choosing && !menuOpen;
            speedText.text = $"{session.GameSpeed:0.#}x";
            pauseText.text = session.IsPaused ? "RESUME" : "PAUSE";
            statusText.text = string.IsNullOrEmpty(session.StatusMessage) ? "Build and upgrade during the assault." : session.StatusMessage;
            hintText.fontSize = IsTargetingSpell ? 18 : 21;
            hintText.text = session.IsPaused ? "PAUSED / plan your defense" : IsTargetingSpell ?
                "Click ground to cast / RMB cancels" : "Pick a tower and pad, then BUILD.";
            fireballButton.interactable = session.CanCastFireball;
            fireballPriceText.text = session.FreeFireballReady ? "FREE CHARGE" : $"{session.FireballCost} MANA";
            fireballPriceText.color = session.FreeFireballReady || session.Mana >= session.FireballCost ? ManaColor : MutedColor;
            fireballStatsText.text = HasAdvancedSpells ? $"{session.FireballDuration:0.#}s BURN / R {session.FireballRadius:0.#}" :
                $"{session.FireballDuration:0.#}s burn / radius {session.FireballRadius:0.#}";
            fireballModeText.text = finished ? "ASSAULT ENDED" : session.IsPaused ? "PAUSED" : fireballTargeting ?
                "CLICK MAP / RMB CANCEL" : !session.FreeFireballReady && session.Mana < session.FireballCost ? $"NEED {session.FireballCost - session.Mana} MANA" : "[Q] AIM";
            fireballModeText.color = fireballTargeting ? new Color(1f, 0.72f, 0.28f) : TextColor;
            fireballBackground.color = fireballTargeting ? new Color(0.32f, 0.14f, 0.055f, 0.97f) : PanelColor;
            frostButton.gameObject.SetActive(session.FrostSpellUnlocked);
            frostButton.interactable = session.FrostSpellUnlocked && session.IsCombatRunning &&
                session.Mana >= session.FrostSpellCost && session.FrostSpellCooldownRemaining <= 0f;
            frostPriceText.text = $"{session.FrostSpellCost} MANA";
            frostPriceText.color = session.Mana >= session.FrostSpellCost ? ManaColor : MutedColor;
            frostStatsText.text = HasAdvancedSpells ? $"{session.FrostSpellDuration:0.#}s FREEZE / R {session.FrostSpellRadius:0.#}" :
                $"{session.FrostSpellDuration:0.#}s freeze / radius {session.FrostSpellRadius:0.#}";
            frostModeText.text = finished ? "ASSAULT ENDED" : session.IsPaused ? "PAUSED" : frostTargeting ?
                "CLICK MAP / RMB CANCEL" : session.FrostSpellCooldownRemaining > 0f ? $"READY IN {Mathf.CeilToInt(session.FrostSpellCooldownRemaining)}s" :
                session.Mana < session.FrostSpellCost ? $"NEED {session.FrostSpellCost - session.Mana} MANA" : "[E] AIM";
            frostModeText.color = frostTargeting ? FrostSpellColor : TextColor;
            frostBackground.color = frostTargeting ? new Color(.055f, .24f, .32f, .97f) : PanelColor;
            RefreshAdvancedSpells();
            for (int i = 0; i < towerButtons.Length; i++)
            {
                bool available = i < AvailableTowerCount;
                towerButtons[i].gameObject.SetActive(available);
                if (!available)
                    continue;
                int cost = session.GetTowerCost((TowerKind)i);
                towerPrices[i].SetText("{0}", cost);
                towerPrices[i].color = session.Gold >= cost ? GoldColor : MutedColor;
                towerButtons[i].interactable = !finished && !choosing && !menuOpen;
                bool selected = !choosing && !IsTargetingSpell && i == (int)selectedKind;
                towerCardBackgrounds[i].color = selected ? new Color(0.13f, 0.26f, 0.35f) : new Color(0.085f, 0.115f, 0.165f);
                UiPolish.SetAccent(towerCardBackgrounds[i], TowerColors[i], selected);
                towerSelectedLabels[i].SetActive(selected);
            }
            bool hasSelection = selectedSlot != null && !finished && !choosing && !menuOpen && !IsTargetingSpell;
            RefreshRangeCircle(hasSelection);
            selectionPanel.SetActive(hasSelection);
            hintPanel.SetActive(!hasSelection && !finished && !choosing);
            if (hasSelection)
                RefreshSelection();
            UpdateAssaultDisplay();
            RefreshBossDisplay();
            RefreshAugments();
            resultPanel.SetActive(finished);
            if (finished)
            {
                resultPanel.transform.SetAsLastSibling();
                bool won = session.Phase == SessionPhase.Victory;
                resultTitle.text = won ? "VICTORY" : "BASE LOST";
                resultTitle.color = won ? new Color(0.40f, 0.94f, 0.76f) : HealthColor;
                resultDescription.text = won ? $"All {session.WaveCount} waves defended.\nBase remaining: {(session.IsCheatMode ? "∞" : session.BaseHealth.ToString())}" :
                    "The enemies reached your base.\nTry frost and blast together.";
                nextLevelButton.gameObject.SetActive(won && session.HasNextLevel);
                nextLevelText.text = session.levelIndex >= 2 ? "NEXT LEVEL / FROZEN FORTRESS" : "NEXT LEVEL / CANYON";
                firstLevelButton.gameObject.SetActive(!session.HasNextLevel && session.FrostSpellUnlocked);
            }
        }

        private void RefreshSelection()
        {
            selectedTower = session.GetTower(selectedSlot);
            if (selectedTower != null)
            {
                int kind = Mathf.Clamp((int)selectedTower.kind, 0, TowerNames.Length - 1);
                slotTitle.text = $"{TowerNames[kind]} / LV {selectedTower.Level}";
                slotTitle.color = TowerColors[kind];
                slotDescription.text = selectedTower.kind == TowerKind.Barracks && selectedTower.Barracks != null ?
                    GetBarracksDescription(selectedTower.Barracks) : selectedTower.kind == TowerKind.Support ?
                    GetSupportDescription(selectedTower) : selectedTower.kind == TowerKind.Cannon ?
                    $"BASE DMG {selectedTower.damage} / R {selectedTower.EffectiveRange:0.#} / AOE {selectedTower.GetShotSpec().splashRadius:0.#}" :
                    $"Base damage {selectedTower.damage}    Range {selectedTower.EffectiveRange:0.#}";
                if (selectedTower.IsMaxLevel) slotDescription.text += "\n" + GetTowerMasteryHint(selectedTower.kind);
                buildButton.interactable = !selectedTower.IsMaxLevel && session.Gold >= selectedTower.UpgradeCost;
                buildText.text = selectedTower.IsMaxLevel ? "MAX LEVEL / LV 3" : session.Gold >= selectedTower.UpgradeCost ?
                    session.Augments.RetrofitPending ? "FREE RETROFIT TO LV 3" :
                    $"UPGRADE TO LV {selectedTower.Level + 1} / {selectedTower.UpgradeCost} GOLD" :
                    $"LV {selectedTower.Level + 1} UPGRADE / NEED {selectedTower.UpgradeCost - session.Gold} GOLD";
            }
            else
            {
                int kind = Mathf.Clamp((int)selectedKind, 0, AvailableTowerCount - 1);
                int cost = session.GetTowerCost(selectedKind);
                TowerAgent prefab = session.towerPrefabs != null && kind < session.towerPrefabs.Length ? session.towerPrefabs[kind] : null;
                slotTitle.text = $"{TowerNames[kind]} / PAD {selectedSlot.slotId}";
                slotTitle.color = TowerColors[kind];
                slotDescription.text = selectedKind == TowerKind.Barracks ?
                    "2 soldiers / upgrades: 3 soldiers + hero / auto respawn" : selectedKind == TowerKind.Support ?
                    "Enemies: vulnerability / allies: heal every second" : prefab != null && selectedKind == TowerKind.Cannon ?
                    $"BASE DMG {prefab.damage} / R {prefab.range + session.Augments.RangeBonus:0.#} / AOE {prefab.GetShotSpec().splashRadius + session.Augments.BlastRadiusBonus:0.#}" :
                    prefab != null ? $"{TowerRoles[kind]}    Range {prefab.range + session.Augments.RangeBonus:0.#}" : TowerRoles[kind];
                buildButton.interactable = session.Gold >= cost;
                buildText.text = session.Gold >= cost ? $"BUILD  /  {cost} GOLD" : $"BUILD  /  NEED {cost - session.Gold} GOLD";
            }
        }

        private static string GetTowerMasteryHint(TowerKind kind) => kind switch
        {
            TowerKind.Gun => "Burst shells: splash 45% / R 1.25",
            TowerKind.Frost => "Iceburst: 1s freeze / R 1.7 / 3s control lock",
            TowerKind.Cannon => "Shatter: 64 DMG / R 4",
            TowerKind.Barracks => "Champion: every 3rd strike spins",
            _ => ""
        };

        private static string GetBarracksDescription(BarracksAgent barracks)
        {
            string respawn = barracks.HeroCapacity > 0 ? $"{barracks.RespawnSeconds:0.#}/{barracks.HeroRespawnSeconds:0.#}s" :
                $"{barracks.RespawnSeconds:0.#}s";
            return $"{barracks.RecruitSummary}\nSoldier HP {barracks.SoldierHealth} / ATK {barracks.SoldierDamage} / revive {respawn}";
        }

        private static string GetSupportDescription(TowerAgent tower)
        {
            SupportTowerAgent aura = tower.Support;
            return aura != null ? $"Vulnerability +{aura.VulnerabilityPercent * 100f:0}% / {aura.VulnerabilityDuration:0.#}s\n" +
                $"Allies heal {aura.HealingPerSecond} HP/s / range {aura.AuraRadius:0.#}" : "Weaken enemies / restore nearby allies";
        }

        private void UpdateAssaultDisplay()
        {
            if (session == null || progressFill == null)
                return;
            // Rect anchors work with a plain UI quad; Image.Filled requires a source sprite.
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(session.AssaultProgress), 1f);
            waveText.text = $"WAVE {session.WaveNumber} / {session.WaveCount}";
            bool finished = IsFinished();
            countdownText.text = finished ? session.Phase == SessionPhase.Victory ? "ASSAULT CLEARED" : "ASSAULT ENDED" :
                session.IsChoosingAugment ? "CHOOSE AN AUGMENT" : session.IsPaused ? "PAUSED" :
                session.NextWaveIn > 0f ? $"NEXT WAVE IN {Mathf.CeilToInt(session.NextWaveIn)}s" : "FINAL ENEMIES";
            for (int i = 0; i < waveMarkers.Count; i++)
                waveMarkers[i].color = i < session.WaveNumber ? new Color(0.98f, 0.76f, 0.26f) : new Color(0.60f, 0.68f, 0.76f);
        }

        private string GetLevelTitle()
        {
            if (session.map == null)
                return "TOWER DEFENSE";
            string id = session.map.levelId ?? "";
            if (id.Contains("Level01")) return "01 / FOREST OUTPOST";
            if (id.Contains("Level02")) return "02 / CANYON CONFLUENCE";
            if (id.Contains("Level03")) return "03 / FROZEN FORTRESS";
            return string.IsNullOrWhiteSpace(id) ? "TOWER DEFENSE" : id;
        }

        private RectTransform Panel(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = color;
            background.raycastTarget = true;
            if (!name.Contains("Overlay") && !name.Contains("Spectrum") && !name.Contains("Track") && !name.Contains("Fill"))
                UiPolish.Skin(background, UiPolish.Trim);
            return rect;
        }

        private TextMeshProUGUI Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color tint)
        {
            RectTransform rect = Rect("Label", parent);
            PlaceTopLeft(rect, position, size);
            return ConfigureText(rect.gameObject.AddComponent<TextMeshProUGUI>(), value, fontSize, tint);
        }

        private TextMeshProUGUI ConfigureText(TextMeshProUGUI text, string value, int fontSize, Color tint)
        {
            if (hudFont != null)
                text.font = hudFont;
            Localization.ApplyFont(text);
            text.text = value;
            text.fontSize = fontSize;
            text.color = tint;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Noto Sans SC's full ascent/descent is taller than its visible glyphs. Ellipsis
            // discards a whole line when that logical height exceeds these compact HUD rows,
            // including plain numeric counters. Allow the font's line metrics to extend beyond
            // the row, as on the menu, without shrinking otherwise readable glyphs.
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.raycastTarget = false;
            // Extra padding expands glyph UVs beyond this font's atlas gutter,
            // producing fragments of adjacent glyphs around otherwise clear text.
            text.extraPadding = false;
            return text;
        }

        private Button Button(Transform parent, string value, Vector2 position, Vector2 size, out TextMeshProUGUI text)
        {
            RectTransform rect = Panel(value.Length > 0 ? value : "Tower Card", parent, AccentColor);
            PlaceTopLeft(rect, position, size);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.83f, 0.88f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.52f, 0.57f, 0.65f, 0.72f);
            button.colors = colors;
            UiPolish.Feedback(button, size.y < 32f ? 1.008f : 1.015f);
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            RectTransform label = Rect("Label", rect);
            Stretch(label);
            label.offsetMin = new Vector2(5f, 0f);
            label.offsetMax = new Vector2(-5f, 0f);
            text = ConfigureText(label.gameObject.AddComponent<TextMeshProUGUI>(), value, 20, TextColor);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private void ResourceChip(Transform parent, float left, float width, Color accent)
        {
            RectTransform chip = Panel("Resource Chip", parent, new Color(.022f, .052f, .069f, .97f));
            PlaceTopLeft(chip, new Vector2(left, -7f), new Vector2(width - 6f, 44f));
            chip.GetComponent<Image>().raycastTarget = false;
            UiPolish.SetAccent(chip.GetComponent<Image>(), accent);
        }

        private void Keycap(Transform parent, string key, Vector2 position, Vector2 size, Color accent)
        {
            RectTransform chip = Panel("Hotkey " + key, parent, new Color(.024f, .056f, .075f, .98f));
            PlaceTopLeft(chip, position, size);
            chip.GetComponent<Image>().raycastTarget = false;
            UiPolish.SetAccent(chip.GetComponent<Image>(), accent);
            var label = Label(chip, key, Vector2.zero, size, 12, accent);
            label.alignment = TextAlignmentOptions.Center;
        }

        private static Image SpriteImage(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect("Model Icon", parent);
            PlaceTopLeft(rect, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static HudIcon Icon(Transform parent, HudIcon.Symbol symbol, Vector2 position, Vector2 size, Color tint)
        {
            RectTransform rect = Rect(symbol + " Icon", parent);
            PlaceTopLeft(rect, position, size);
            HudIcon icon = rect.gameObject.AddComponent<HudIcon>();
            icon.symbol = symbol;
            icon.color = tint;
            icon.raycastTarget = false;
            return icon;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void AnchorTopRight(RectTransform rect) => rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;

        private static void AnchorBottomLeft(RectTransform rect, Vector2 size, float left, float bottom)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(left, bottom);
        }

        private static void AnchorBottomRight(RectTransform rect, Vector2 size, float right, float bottom)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(-right, bottom);
        }
    }
}
