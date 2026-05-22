using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class TurretDataManagerWindow : EditorWindow
{
    private int activeTab = 0; // 0: Turrets, 1: Enemies, 2: Waves

    // Asset Lists
    private List<TurretDataSO> turretAssets = new List<TurretDataSO>();
    private List<EnemyDataSO> enemyAssets = new List<EnemyDataSO>();
    private List<WaveDataSO> waveAssets = new List<WaveDataSO>();

    // Current Selections
    private TurretDataSO selectedTurret;
    private EnemyDataSO selectedEnemy;
    private WaveDataSO selectedWave;

    // Scroll States
    private Vector2 sidebarScrollPos;
    private Vector2 detailsScrollPos;

    // Interactive Preview State (Turrets)
    private int previewLevel = 1;

    [MenuItem("Window/Turret & Wave Data Manager")]
    public static void ShowWindow()
    {
        TurretDataManagerWindow window = GetWindow<TurretDataManagerWindow>("Data Manager");
        window.titleContent = new GUIContent("Data Manager", EditorGUIUtility.IconContent("d_UnityEditor.GameView").image);
        window.minSize = new Vector2(800, 500);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshAllAssetLists();
    }

    private void RefreshAllAssetLists()
    {
        RefreshTurretList();
        RefreshEnemyList();
        RefreshWaveList();
    }

    private void RefreshTurretList()
    {
        turretAssets.Clear();
        string[] guids = AssetDatabase.FindAssets("t:TurretDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TurretDataSO asset = AssetDatabase.LoadAssetAtPath<TurretDataSO>(path);
            if (asset != null) turretAssets.Add(asset);
        }
        if (selectedTurret != null && !turretAssets.Contains(selectedTurret)) selectedTurret = null;
    }

    private void RefreshEnemyList()
    {
        enemyAssets.Clear();
        string[] guids = AssetDatabase.FindAssets("t:EnemyDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyDataSO asset = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            if (asset != null) enemyAssets.Add(asset);
        }
        if (selectedEnemy != null && !enemyAssets.Contains(selectedEnemy)) selectedEnemy = null;
    }

    private void RefreshWaveList()
    {
        waveAssets.Clear();
        string[] guids = AssetDatabase.FindAssets("t:WaveDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WaveDataSO asset = AssetDatabase.LoadAssetAtPath<WaveDataSO>(path);
            if (asset != null) waveAssets.Add(asset);
        }
        if (selectedWave != null && !waveAssets.Contains(selectedWave)) selectedWave = null;
    }

    private void OnGUI()
    {
        DrawTitleBarAndTabs();

        GUILayout.BeginHorizontal();

        // Left Sidebar based on active tab
        DrawSidebar();

        // Right Detail Panel based on active tab
        DrawDetailsPanel();

        GUILayout.EndHorizontal();

        if (GUI.changed)
        {
            Repaint();
        }
    }

    private void DrawTitleBarAndTabs()
    {
        // Title block
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Space(10);
        
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft
        };
        titleStyle.normal.textColor = new Color(0.25f, 0.75f, 1f);

        GUILayout.Label("⚔️ Battle Balance Studio", titleStyle, GUILayout.Height(35));
        
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("🔄 Refresh Database", GUILayout.Width(130), GUILayout.Height(25)))
        {
            RefreshAllAssetLists();
        }
        GUILayout.Space(5);
        GUILayout.EndHorizontal();

        // Tabs Toolbar
        int prevTab = activeTab;
        string[] tabNames = { "🛡 Turret Database", "👾 Enemy Templates", "🌊 Wave Scenarios" };
        activeTab = GUILayout.Toolbar(activeTab, tabNames, GUILayout.Height(30));
        if (prevTab != activeTab)
        {
            GUI.FocusControl(null);
            sidebarScrollPos = Vector2.zero;
            detailsScrollPos = Vector2.zero;
        }

        GUILayout.EndVertical();
    }

    private void DrawSidebar()
    {
        GUILayout.BeginVertical(GUILayout.Width(240), GUILayout.ExpandHeight(true));
        
        string headerText = activeTab == 0 ? "Turret Templates" : activeTab == 1 ? "Enemy Templates" : "Wave Scenarios";
        GUILayout.Box(headerText, GUILayout.ExpandWidth(true), GUILayout.Height(22));

        sidebarScrollPos = GUILayout.BeginScrollView(sidebarScrollPos, GUI.skin.box, GUILayout.ExpandHeight(true));

        if (activeTab == 0)
        {
            DrawTurretsSidebarList();
        }
        else if (activeTab == 1)
        {
            DrawEnemiesSidebarList();
        }
        else
        {
            DrawWavesSidebarList();
        }

        GUILayout.EndScrollView();

        // Sidebar Footer Control Buttons
        DrawSidebarFooterButtons();

        GUILayout.EndVertical();
    }

    private void DrawTurretsSidebarList()
    {
        for (int i = 0; i < turretAssets.Count; i++)
        {
            TurretDataSO turret = turretAssets[i];
            if (turret == null) continue;

            Color oldBg = GUI.backgroundColor;
            if (selectedTurret == turret) GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);

            GUILayout.BeginHorizontal();
            Texture2D iconTex = turret.icon != null ? AssetPreview.GetAssetPreview(turret.icon) : null;
            if (iconTex != null)
                GUILayout.Box(iconTex, GUILayout.Width(24), GUILayout.Height(24));
            else
                GUILayout.Box("🛡", GUILayout.Width(24), GUILayout.Height(24));

            if (GUILayout.Button(turret.turretName, EditorStyles.label, GUILayout.Height(24), GUILayout.ExpandWidth(true)))
            {
                selectedTurret = turret;
                previewLevel = 1;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            GUI.backgroundColor = oldBg;
        }
    }

    private void DrawEnemiesSidebarList()
    {
        for (int i = 0; i < enemyAssets.Count; i++)
        {
            EnemyDataSO enemy = enemyAssets[i];
            if (enemy == null) continue;

            Color oldBg = GUI.backgroundColor;
            if (selectedEnemy == enemy) GUI.backgroundColor = new Color(0.9f, 0.4f, 0.2f);

            GUILayout.BeginHorizontal();
            Texture2D iconTex = enemy.enemySprite != null ? AssetPreview.GetAssetPreview(enemy.enemySprite) : null;
            if (iconTex != null)
                GUILayout.Box(iconTex, GUILayout.Width(24), GUILayout.Height(24));
            else
                GUILayout.Box("👾", GUILayout.Width(24), GUILayout.Height(24));

            if (GUILayout.Button(enemy.enemyName, EditorStyles.label, GUILayout.Height(24), GUILayout.ExpandWidth(true)))
            {
                selectedEnemy = enemy;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            GUI.backgroundColor = oldBg;
        }
    }

    private void DrawWavesSidebarList()
    {
        for (int i = 0; i < waveAssets.Count; i++)
        {
            WaveDataSO wave = waveAssets[i];
            if (wave == null) continue;

            Color oldBg = GUI.backgroundColor;
            if (selectedWave == wave) GUI.backgroundColor = new Color(0.2f, 0.7f, 0.4f);

            GUILayout.BeginHorizontal();
            GUILayout.Box("🌊", GUILayout.Width(24), GUILayout.Height(24));

            if (GUILayout.Button(wave.waveName, EditorStyles.label, GUILayout.Height(24), GUILayout.ExpandWidth(true)))
            {
                selectedWave = wave;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            GUI.backgroundColor = oldBg;
        }
    }

    private void DrawSidebarFooterButtons()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        if (activeTab == 0)
        {
            if (GUILayout.Button("➕ Create New Turret", GUILayout.Height(28))) CreateNewTurretAsset();
            if (selectedTurret != null)
            {
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("🗑 Delete Selected Turret", GUILayout.Height(22))) DeleteSelectedTurretAsset();
                GUI.backgroundColor = oldBg;
            }
        }
        else if (activeTab == 1)
        {
            if (GUILayout.Button("➕ Create New Enemy Template", GUILayout.Height(28))) CreateNewEnemyAsset();
            if (selectedEnemy != null)
            {
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("🗑 Delete Selected Enemy", GUILayout.Height(22))) DeleteSelectedEnemyAsset();
                GUI.backgroundColor = oldBg;
            }
        }
        else
        {
            if (GUILayout.Button("➕ Create New Wave Scenario", GUILayout.Height(28))) CreateNewWaveAsset();
            if (selectedWave != null)
            {
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("🗑 Delete Selected Wave", GUILayout.Height(22))) DeleteSelectedWaveAsset();
                GUI.backgroundColor = oldBg;
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawDetailsPanel()
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (activeTab == 0)
        {
            DrawTurretsDetails();
        }
        else if (activeTab == 1)
        {
            DrawEnemiesDetails();
        }
        else
        {
            DrawWavesDetails();
        }

        GUILayout.EndVertical();
    }

    private void DrawTurretsDetails()
    {
        if (selectedTurret == null)
        {
            DrawSelectionPrompt("Select a turret asset from the sidebar\nor click 'Create New Turret' to start.");
            return;
        }

        SerializedObject so = new SerializedObject(selectedTurret);
        so.Update();

        detailsScrollPos = GUILayout.BeginScrollView(detailsScrollPos);

        DrawDetailHeader($"✏ Editing Turret: {selectedTurret.turretName}", selectedTurret);

        // Group 1: General Info
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("General Info", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("turretName"));
        EditorGUILayout.PropertyField(so.FindProperty("weaponPrefab"));
        EditorGUILayout.PropertyField(so.FindProperty("icon"));
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 1.5: Weapon & Targeting Settings
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("🏹 Weapon & Targeting Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("attackOnlyDuringDefense"));
        EditorGUILayout.PropertyField(so.FindProperty("targetRefreshInterval"));
        EditorGUILayout.PropertyField(so.FindProperty("projectilePrefab"));
        EditorGUILayout.PropertyField(so.FindProperty("projectileSpeed"));
        GUILayout.EndVertical();
        EditorGUILayout.Space();

        // Group 1.6: Visual Effect Settings
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("✨ Visual Effect Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("explosionSprite"));
        EditorGUILayout.PropertyField(so.FindProperty("explosionColor"));
        GUILayout.EndVertical();
        EditorGUILayout.Space();

        // Group 1.75: Attack Mode Settings
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("⚔️ Attack Mode Settings", EditorStyles.boldLabel);
        SerializedProperty attackTypeProp = so.FindProperty("attackType");
        EditorGUILayout.PropertyField(attackTypeProp);

        if (attackTypeProp != null)
        {
            TurretAttackType attackType = (TurretAttackType)attackTypeProp.enumValueIndex;
            
            if (attackType == TurretAttackType.AOE || attackType == TurretAttackType.AOESlow)
            {
                EditorGUILayout.PropertyField(so.FindProperty("splashRadius"));
            }
            
            if (attackType == TurretAttackType.AOESlow)
            {
                EditorGUILayout.PropertyField(so.FindProperty("slowFactor"));
                EditorGUILayout.PropertyField(so.FindProperty("slowDuration"));
            }
            
            if (attackType == TurretAttackType.AuraDamage)
            {
                EditorGUILayout.PropertyField(so.FindProperty("auraActiveDuration"));
                EditorGUILayout.PropertyField(so.FindProperty("auraRestDuration"));
            }
            
            if (attackType == TurretAttackType.Shotgun)
            {
                EditorGUILayout.PropertyField(so.FindProperty("shotgunPelletCount"));
                EditorGUILayout.PropertyField(so.FindProperty("shotgunSpreadAngle"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(so.FindProperty("shotgunPelletUpgrades"), true);
            }
            
            if (attackType == TurretAttackType.Piercing)
            {
                EditorGUILayout.PropertyField(so.FindProperty("basePierceCount"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(so.FindProperty("pierceUpgrades"), true);
            }
        }
        GUILayout.EndVertical();

        EditorGUILayout.Space();
        // Group 2: Base Stats
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("Base Level 1 Stats", EditorStyles.boldLabel);
        SerializedProperty baseStatsProp = so.FindProperty("baseStats");
        if (baseStatsProp != null)
        {
            EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("damage"));
            EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("fireRate"));
            EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("range"));
        }
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 3: Growth Factors
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("Level Growth Scaling Rates (Multiplier Per Level)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("damageGrowthPerLevel"));
        EditorGUILayout.PropertyField(so.FindProperty("fireRateGrowthPerLevel"));
        EditorGUILayout.PropertyField(so.FindProperty("rangeGrowthPerLevel"));
        EditorGUILayout.PropertyField(so.FindProperty("splashRadiusGrowthPerLevel"));
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 3.5: Visual Upgrades
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("🎨 Level-based Visual Upgrades", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("visualUpgrades"), true);
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Apply changes to the serialized object before simulator calculations
        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(selectedTurret);
            SyncAssetName(selectedTurret, selectedTurret.turretName);
            RefreshTurretList();
            Repaint();
        }

        // Group 4: Interactive Stats Simulator
        GUILayout.BeginVertical("GroupBox");
        GUIStyle simulatorHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
        simulatorHeaderStyle.normal.textColor = new Color(0.2f, 0.8f, 0.4f);
        EditorGUILayout.LabelField("📈 Real-time Level Stats Simulator", simulatorHeaderStyle);
        
        previewLevel = EditorGUILayout.IntSlider("Simulated Level", previewLevel, 1, 10);
        
        TurretStats scaledStats = selectedTurret.GetStatsForLevel(previewLevel);
        Sprite previewSprite = selectedTurret.GetSpriteForLevel(previewLevel);
        
        EditorGUILayout.Space();
        
        GUIStyle statStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
        EditorGUILayout.LabelField($"Simulated Stats & Visuals at Level {previewLevel}:", EditorStyles.miniBoldLabel);
        
        GUILayout.BeginHorizontal();
        Texture2D spriteTex = previewSprite != null ? AssetPreview.GetAssetPreview(previewSprite) : null;
        if (spriteTex != null)
            GUILayout.Box(spriteTex, GUILayout.Width(50), GUILayout.Height(50));
        else
            GUILayout.Box("🛡", GUILayout.Width(50), GUILayout.Height(50));
        
        GUILayout.BeginVertical();
        float dps = scaledStats.damage * scaledStats.fireRate;
        if (selectedTurret.attackType == TurretAttackType.Shotgun)
        {
            dps *= selectedTurret.GetShotgunPelletCountForLevel(previewLevel);
        }
        GUILayout.Label($"💥 Damage: {scaledStats.damage:F1}", statStyle);
        GUILayout.Label($"⚡ Fire Rate: {scaledStats.fireRate:F2}/s", statStyle);
        GUILayout.Label($"🔥 DPS: {dps:F1}", statStyle);
        GUILayout.Label($"🎯 Range: {scaledStats.range:F1}m", statStyle);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 5: Combat Behavior Preview
        DrawCombatPreview(scaledStats);


        GUILayout.EndScrollView();
    }

    private void DrawCombatPreview(TurretStats stats)
    {
        GUILayout.BeginVertical("GroupBox");
        GUIStyle previewHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
        previewHeaderStyle.normal.textColor = new Color(0.9f, 0.6f, 0.2f);
        EditorGUILayout.LabelField("🎥 Live Combat Behavior Preview", previewHeaderStyle);
        EditorGUILayout.Space();

        Rect rect = GUILayoutUtility.GetRect(300, 300, GUILayout.ExpandWidth(true));
        
        if (Event.current.type == EventType.Repaint)
        {
            GUI.BeginClip(rect);

            // 1. Draw Background
            EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height), new Color(0.12f, 0.12f, 0.12f));
            
            // Draw Grid Lines (optional for scale)
            Handles.color = new Color(0.2f, 0.2f, 0.2f);
            for(int i = 0; i < 15; i++)
            {
                float step = rect.width / 15f;
                Handles.DrawLine(new Vector3(i * step, 0, 0), new Vector3(i * step, rect.height, 0));
                Handles.DrawLine(new Vector3(0, i * step, 0), new Vector3(rect.width, i * step, 0));
            }

            Vector3 center = new Vector3(rect.width / 2f, rect.height / 2f, 0f);
            
            // Determine fixed scale based on Max Level (10) so the circle visibly grows when leveling up
            float maxPossibleRange = selectedTurret.GetStatsForLevel(10).range;
            float maxRangeDisplay = Mathf.Max(maxPossibleRange, selectedTurret.splashRadius, 1f);
            float pixelsPerMeter = (rect.width / 2f * 0.8f) / maxRangeDisplay;
            
            float rangeRadius = stats.range * pixelsPerMeter;

            // 2. Draw Attack Range
            Handles.color = new Color(0.3f, 0.8f, 0.3f, 0.5f);
            Handles.DrawWireDisc(center, Vector3.forward, rangeRadius);
            Handles.color = new Color(0.3f, 0.8f, 0.3f, 0.05f);
            Handles.DrawSolidDisc(center, Vector3.forward, rangeRadius);

            // 3. Draw Attack Type Specific Visuals
            TurretAttackType attackType = selectedTurret.attackType;
            Vector3 targetDir = new Vector3(0.707f, -0.707f, 0f); // Top right (45 degrees in GUI coords, Y is down)
            Vector3 targetPos = center + targetDir * rangeRadius;

            if (attackType == TurretAttackType.SingleTarget)
            {
                Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
                Handles.DrawDottedLine(center, targetPos, 2f);
                Handles.DrawSolidDisc(targetPos, Vector3.forward, 4f);
            }
            else if (attackType == TurretAttackType.AOE || attackType == TurretAttackType.AOESlow)
            {
                Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
                Handles.DrawDottedLine(center, targetPos, 2f);
                
                float splashPixelRadius = selectedTurret.splashRadius * pixelsPerMeter;
                
                if (attackType == TurretAttackType.AOESlow)
                    Handles.color = new Color(0.3f, 0.7f, 1f, 0.6f); // Blue for slow
                else
                    Handles.color = new Color(1f, 0.4f, 0.1f, 0.6f); // Orange for AOE
                    
                Handles.DrawSolidDisc(targetPos, Vector3.forward, splashPixelRadius);
                Handles.color = Color.white;
                Handles.DrawWireDisc(targetPos, Vector3.forward, splashPixelRadius);
            }
            else if (attackType == TurretAttackType.AuraDamage)
            {
                Handles.color = new Color(1f, 0.5f, 0f, 0.25f);
                Handles.DrawSolidDisc(center, Vector3.forward, rangeRadius);
                Handles.color = new Color(1f, 0.8f, 0f, 0.8f);
                Handles.DrawWireDisc(center, Vector3.forward, rangeRadius);
            }
            else if (attackType == TurretAttackType.Shotgun)
            {
                float spread = selectedTurret.shotgunSpreadAngle;
                Handles.color = new Color(1f, 0.3f, 0.3f, 0.4f);
                
                Vector3 fromDir = Quaternion.Euler(0, 0, -spread / 2f) * targetDir;
                Handles.DrawSolidArc(center, Vector3.forward, fromDir, spread, rangeRadius);
                
                Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
                Handles.DrawWireArc(center, Vector3.forward, fromDir, spread, rangeRadius);
                Handles.DrawLine(center, center + fromDir * rangeRadius);
                Handles.DrawLine(center, center + (Quaternion.Euler(0, 0, spread) * fromDir) * rangeRadius);
            }
            else if (attackType == TurretAttackType.Piercing)
            {
                Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                Handles.DrawDottedLine(center, targetPos, 2f);
                
                // Draw extended line to show piercing behavior
                Vector3 pierceEnd = center + targetDir * rangeRadius * 1.5f;
                Handles.color = new Color(1f, 0.2f, 0.2f, 0.4f);
                Handles.DrawDottedLine(targetPos, pierceEnd, 2f);
                
                // Draw imaginary targets pierced
                Handles.color = new Color(1f, 0.8f, 0.8f, 0.8f);
                for(int i = 1; i <= 3; i++) {
                    Vector3 pt = center + targetDir * (rangeRadius * (i * 0.4f));
                    Handles.DrawSolidDisc(pt, Vector3.forward, 3f);
                }
            }

            // 4. Draw Turret Icon in Center
            Texture2D tex = selectedTurret.icon != null ? AssetPreview.GetAssetPreview(selectedTurret.icon) : null;
            if (tex != null)
            {
                GUI.DrawTexture(new Rect(center.x - 16, center.y - 16, 32, 32), tex);
            }
            else
            {
                GUI.Label(new Rect(center.x - 10, center.y - 10, 20, 20), "🛡");
            }

            GUI.EndClip();
        }

        GUILayout.EndVertical();
    }

    private void DrawEnemiesDetails()
    {
        if (selectedEnemy == null)
        {
            DrawSelectionPrompt("Select an enemy asset from the sidebar\nor click 'Create New Enemy Template' to start.");
            return;
        }

        SerializedObject so = new SerializedObject(selectedEnemy);
        so.Update();

        detailsScrollPos = GUILayout.BeginScrollView(detailsScrollPos);

        DrawDetailHeader($"✏ Editing Enemy: {selectedEnemy.enemyName}", selectedEnemy);

        // Group 1: Identity Settings
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("👾 Identity Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("enemyName"));
        
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        EditorGUILayout.PropertyField(so.FindProperty("enemySprite"));
        EditorGUILayout.PropertyField(so.FindProperty("enemyPrefab"));
        GUILayout.EndVertical();
        
        if (selectedEnemy.enemySprite != null)
        {
            Texture2D spriteTex = AssetPreview.GetAssetPreview(selectedEnemy.enemySprite);
            if (spriteTex != null)
            {
                GUILayout.Box(spriteTex, GUILayout.Width(64), GUILayout.Height(64));
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 2: Base Stats
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("📈 Health & Speed Stats", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("maxHealth"));
        EditorGUILayout.PropertyField(so.FindProperty("moveSpeed"));
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 3: Attack Stats
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("⚔️ Attack Parameters", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("contactDamage"));
        EditorGUILayout.PropertyField(so.FindProperty("attackInterval"));
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 4: Economy & Rewards
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("💰 Drop Rewards", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("currencyReward"));
        EditorGUILayout.PropertyField(so.FindProperty("blockDropChance"));
        GUILayout.EndVertical();

        GUILayout.EndScrollView();

        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(selectedEnemy);
            SyncAssetName(selectedEnemy, selectedEnemy.enemyName);
            RefreshEnemyList();
        }
    }

    private void DrawWavesDetails()
    {
        if (selectedWave == null)
        {
            DrawSelectionPrompt("Select a wave asset from the sidebar\nor click 'Create New Wave Scenario' to start.");
            return;
        }

        SerializedObject so = new SerializedObject(selectedWave);
        so.Update();

        detailsScrollPos = GUILayout.BeginScrollView(detailsScrollPos);

        DrawDetailHeader($"✏ Editing Wave Scenario: {selectedWave.waveName}", selectedWave);

        // Group 1: Wave Settings
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("🌊 Wave Configuration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("waveName"));
        EditorGUILayout.PropertyField(so.FindProperty("defenseDuration"));
        EditorGUILayout.PropertyField(so.FindProperty("spawnInterval"));
        EditorGUILayout.PropertyField(so.FindProperty("maxAliveEnemies"));
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 1.1: Circular Spawn Settings
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("⭕ Circular Spawn Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("spawnRadiusMin"));
        EditorGUILayout.PropertyField(so.FindProperty("spawnRadiusMax"));
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 1.2: Surge (Swarm) Settings
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("🐜 Surge (Swarm) Spawn Settings", EditorStyles.boldLabel);
        SerializedProperty useSurgeProp = so.FindProperty("useSurgeSpawn");
        EditorGUILayout.PropertyField(useSurgeProp);
        if (useSurgeProp.boolValue)
        {
            EditorGUILayout.PropertyField(so.FindProperty("surgeInterval"));
            EditorGUILayout.PropertyField(so.FindProperty("surgeDirections"));
            EditorGUILayout.PropertyField(so.FindProperty("surgeSpawnPerDirection"));
            EditorGUILayout.PropertyField(so.FindProperty("surgeClusterRadius"));
        }
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 1.5: Burst Spawn Settings
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("💥 Legacy Burst Spawn Settings", EditorStyles.boldLabel);
        SerializedProperty useBurstProp = so.FindProperty("useBurstSpawn");
        EditorGUILayout.PropertyField(useBurstProp);
        if (useBurstProp.boolValue)
        {
            EditorGUILayout.PropertyField(so.FindProperty("burstInterval"));
            EditorGUILayout.PropertyField(so.FindProperty("burstCount"));
        }
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Group 2: Spawn List configuration
        GUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField("👾 Spawn List Entries", EditorStyles.boldLabel);
        
        SerializedProperty listProp = so.FindProperty("enemiesToSpawn");
        if (listProp != null)
        {
            EditorGUILayout.LabelField($"Total Distinct Spawn Types: {listProp.arraySize}", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty elementProp = listProp.GetArrayElementAtIndex(i);
                SerializedProperty enemyDataProp = elementProp.FindPropertyRelative("enemyData");
                SerializedProperty weightProp = elementProp.FindPropertyRelative("weight");

                GUILayout.BeginHorizontal("HelpBox");
                
                // Draw enemy asset slot
                EditorGUILayout.PropertyField(enemyDataProp, GUIContent.none, GUILayout.Width(250));
                
                GUILayout.Space(10);
                
                // Draw quantity editor
                EditorGUILayout.LabelField("Weight:", GUILayout.Width(50));
                weightProp.intValue = EditorGUILayout.IntField(weightProp.intValue, GUILayout.Width(60));
                weightProp.intValue = Mathf.Max(1, weightProp.intValue);

                GUILayout.FlexibleSpace();

                // Delete Entry Button
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    listProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                GUI.backgroundColor = oldBg;

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            // Add Entry Button
            if (GUILayout.Button("➕ Add Enemy Spawn Entry", GUILayout.Height(24)))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                SerializedProperty newElement = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newElement.FindPropertyRelative("enemyData").objectReferenceValue = null;
                newElement.FindPropertyRelative("weight").intValue = 5;
            }
        }

        GUILayout.EndVertical();

        GUILayout.EndScrollView();

        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(selectedWave);
            SyncAssetName(selectedWave, selectedWave.waveName);
            RefreshWaveList();
        }
    }

    private void DrawDetailHeader(string text, ScriptableObject obj)
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        headerStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        GUILayout.Label(text, headerStyle);

        string assetPath = AssetDatabase.GetAssetPath(obj);
        EditorGUILayout.LabelField("Asset Path:", assetPath, EditorStyles.miniLabel);
        EditorGUILayout.Space();
    }

    private void DrawSelectionPrompt(string prompt)
    {
        GUILayout.FlexibleSpace();
        GUIStyle selectMsgStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 13,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        selectMsgStyle.normal.textColor = Color.gray;
        GUILayout.Label(prompt, selectMsgStyle);
        GUILayout.FlexibleSpace();
    }

    // Asset Creation Helpers
    private void CreateNewTurretAsset()
    {
        string directory = "Assets/Data/Turrets";
        EnsureDirectoryExists(directory);
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(directory + "/NewTurretData.asset");
        TurretDataSO asset = CreateInstance<TurretDataSO>();
        asset.turretName = Path.GetFileNameWithoutExtension(uniquePath);

        AssetDatabase.CreateAsset(asset, uniquePath);
        SaveAndRefresh();
        RefreshTurretList();
        selectedTurret = asset;
    }

    private void CreateNewEnemyAsset()
    {
        string directory = "Assets/Data/Enemies";
        EnsureDirectoryExists(directory);
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(directory + "/NewEnemyData.asset");
        EnemyDataSO asset = CreateInstance<EnemyDataSO>();
        asset.enemyName = Path.GetFileNameWithoutExtension(uniquePath);

        AssetDatabase.CreateAsset(asset, uniquePath);
        SaveAndRefresh();
        RefreshEnemyList();
        selectedEnemy = asset;
    }

    private void CreateNewWaveAsset()
    {
        string directory = "Assets/Data/Waves";
        EnsureDirectoryExists(directory);
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(directory + "/NewWaveData.asset");
        WaveDataSO asset = CreateInstance<WaveDataSO>();
        asset.waveName = Path.GetFileNameWithoutExtension(uniquePath);

        AssetDatabase.CreateAsset(asset, uniquePath);
        SaveAndRefresh();
        RefreshWaveList();
        selectedWave = asset;
    }

    // Asset Deletion Helpers
    private void DeleteSelectedTurretAsset()
    {
        if (selectedTurret == null) return;
        string path = AssetDatabase.GetAssetPath(selectedTurret);
        if (EditorUtility.DisplayDialog("Delete Turret Template", $"Are you sure you want to delete {selectedTurret.turretName}?\nThis action cannot be undone.", "Delete", "Cancel"))
        {
            AssetDatabase.DeleteAsset(path);
            SaveAndRefresh();
            selectedTurret = null;
            RefreshTurretList();
        }
    }

    private void DeleteSelectedEnemyAsset()
    {
        if (selectedEnemy == null) return;
        string path = AssetDatabase.GetAssetPath(selectedEnemy);
        if (EditorUtility.DisplayDialog("Delete Enemy Template", $"Are you sure you want to delete {selectedEnemy.enemyName}?\nThis action cannot be undone.", "Delete", "Cancel"))
        {
            AssetDatabase.DeleteAsset(path);
            SaveAndRefresh();
            selectedEnemy = null;
            RefreshEnemyList();
        }
    }

    private void DeleteSelectedWaveAsset()
    {
        if (selectedWave == null) return;
        string path = AssetDatabase.GetAssetPath(selectedWave);
        if (EditorUtility.DisplayDialog("Delete Wave Scenario", $"Are you sure you want to delete {selectedWave.waveName}?\nThis action cannot be undone.", "Delete", "Cancel"))
        {
            AssetDatabase.DeleteAsset(path);
            SaveAndRefresh();
            selectedWave = null;
            RefreshWaveList();
        }
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private void SaveAndRefresh()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void SyncAssetName(ScriptableObject asset, string desiredName)
    {
        if (asset == null || string.IsNullOrEmpty(desiredName)) return;

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path)) return;

        string currentFileName = Path.GetFileNameWithoutExtension(path);
        if (currentFileName != desiredName)
        {
            string sanitizedName = desiredName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sanitizedName = sanitizedName.Replace(c, '_');
            }

            if (sanitizedName != currentFileName && !string.IsNullOrEmpty(sanitizedName))
            {
                string error = AssetDatabase.RenameAsset(path, sanitizedName);
                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log($"Renamed asset '{currentFileName}' to '{sanitizedName}' at path: {path}");
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogWarning($"Failed to rename asset '{currentFileName}' to '{sanitizedName}': {error}");
                }
            }
        }
    }
}

