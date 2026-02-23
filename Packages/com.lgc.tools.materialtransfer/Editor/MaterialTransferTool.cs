#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

public class MaterialTransferTool : EditorWindow
{
    // 扩展模式选择，增加LilToon批量创建模式
    private enum ToolMode { MaterialTransfer, MaterialVariant, LilToonBatchCreator }
    private ToolMode currentMode = ToolMode.MaterialTransfer;

    // ==================== 原有 MaterialTransferTool 变量 ====================
    // 材质转移模式变量
    private GameObject sourceObject;
    private GameObject targetObject;
    private List<RendererInfo> sourceRenderers = new List<RendererInfo>();
    private List<RendererInfo> targetRenderers = new List<RendererInfo>();
    private Vector2 sourceScrollPos;
    private Vector2 targetScrollPos;

    // 材质变体模式变量
    private DefaultAsset materialFolder;
    private List<Material> variantMaterials = new List<Material>();

    // 通用变量
    private Vector2 scrollPos;
    private bool showMaterialPreview = true;
    private bool useRelativePath = true;
    private bool matchByName = true;
    private bool matchByHierarchy = true;
    private bool showOnlyMatching = false;
    private bool showOnlyUnmatching = false;

    // 样式
    private GUIStyle warningStyle;
    private GUIStyle autoMatchedStyle;
    private GUIStyle pathStyle;
    private GUIStyle headerStyle;
    private GUIStyle centeredLabelStyle;
    private GUIStyle tabStyle;

    // ==================== LilToonBatchMaterialCreator 变量 ====================
    // 输出路径固定为指定路径
    private string outputPath = "Assets/LGC/Tools/批量材质创建动画/生成的材质";
    // 错误/成功提示
    private string errorMessage = "";
    private string successMessage = "";

    // 批量文件夹配置
    private UnityEngine.Object batchImageFolder;

    // 固定图片配置 - 默认绑定主色1（0）
    private bool enableFixedTexture = false;
    private Texture2D fixedTexture;
    private int fixedChannelSelect = 0;
    private List<int> fixedAvailableChannelIndices = new List<int>();
    private List<string> fixedAvailableChannelNames = new List<string>();

    // 模板材质核心配置
    private bool enableTemplateMaterial = false;
    private Material templateMaterial;
    private List<int> templateUsedChannels = new List<int>();
    private bool enableCoverTemplateChannel = false; // 允许固定图片覆盖模板已用槽位

    // 批量图片槽位配置
    private int batchChannelSelect = 0;
    private List<int> batchAvailableChannelIndices = new List<int>();
    private List<string> batchAvailableChannelNames = new List<string>();

    // 混合模式配置（主色2/3用）
    private int blendMode2nd = 0;
    private int blendMode3rd = 0;

    // 静态常量配置（LilToon主色槽位映射）
    private readonly string[] _mainChannelNames = { "主色1 (MainTex)", "主色2 (Main2ndTex)", "主色3 (Main3rdTex)" };
    private readonly string[] _mainChannelTexProps = { "_MainTex", "_Main2ndTex", "_Main3rdTex" };
    private readonly string[] _blendModeNames = { "Normal (正常)", "Add (叠加)", "Screen (滤色)", "Multiply (正片叠底)" };
    private readonly string LIL_TOON_SHADER_KEY = "lilToon";

    [MenuItem("LGC/材质工具")]
    public static void ShowWindow()
    {
        GetWindow<MaterialTransferTool>("材质工具");
    }

    private void OnEnable()
    {
        // 初始化原有样式
        warningStyle = new GUIStyle(EditorStyles.miniLabel);
        warningStyle.normal.textColor = Color.red;

        autoMatchedStyle = new GUIStyle(EditorStyles.miniLabel);
        autoMatchedStyle.normal.textColor = new Color(0.2f, 0.7f, 0.2f);

        pathStyle = new GUIStyle(EditorStyles.miniLabel);
        pathStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

        headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
        headerStyle.alignment = TextAnchor.MiddleCenter;

        centeredLabelStyle = new GUIStyle(EditorStyles.label);
        centeredLabelStyle.alignment = TextAnchor.MiddleCenter;

        tabStyle = new GUIStyle(EditorStyles.toolbarButton);
        tabStyle.fixedHeight = 25;
    }

    private void OnGUI()
    {
        GUILayout.Label("材质工具总面板", headerStyle);

        // 扩展模式选择标签，增加第三个LilToon批量创建按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentMode == ToolMode.MaterialTransfer, "物体材质转移模式", tabStyle))
            currentMode = ToolMode.MaterialTransfer;
        if (GUILayout.Toggle(currentMode == ToolMode.MaterialVariant, "文件夹材质转移模式", tabStyle))
            currentMode = ToolMode.MaterialVariant;
        if (GUILayout.Toggle(currentMode == ToolMode.LilToonBatchCreator, "LilToon批量材质创建", tabStyle))
            currentMode = ToolMode.LilToonBatchCreator;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 根据当前模式渲染对应UI
        switch (currentMode)
        {
            case ToolMode.MaterialTransfer:
                RenderMaterialTransferMode();
                break;
            case ToolMode.MaterialVariant:
                RenderMaterialVariantMode();
                break;
            case ToolMode.LilToonBatchCreator:
                RenderLilToonBatchCreatorMode();
                break;
        }
    }

    // ==================== 原有 MaterialTransfer 模式渲染 ====================
    private void RenderMaterialTransferMode()
    {
        // 原有代码保持不变
        // 匹配设置
        EditorGUILayout.BeginVertical("Box");
        GUILayout.Label("匹配设置", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        matchByName = EditorGUILayout.ToggleLeft("按名称匹配", matchByName, GUILayout.Width(120));
        matchByHierarchy = EditorGUILayout.ToggleLeft("按层级匹配", matchByHierarchy, GUILayout.Width(120));
        useRelativePath = EditorGUILayout.ToggleLeft("使用相对路径", useRelativePath, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        showMaterialPreview = EditorGUILayout.ToggleLeft("显示材质预览", showMaterialPreview, GUILayout.Width(120));

        EditorGUI.BeginChangeCheck();
        showOnlyMatching = EditorGUILayout.ToggleLeft("只显示匹配项", showOnlyMatching, GUILayout.Width(120));
        if (EditorGUI.EndChangeCheck() && showOnlyMatching)
            showOnlyUnmatching = false;

        EditorGUI.BeginChangeCheck();
        showOnlyUnmatching = EditorGUILayout.ToggleLeft("只显示未匹配项", showOnlyUnmatching, GUILayout.Width(120));
        if (EditorGUI.EndChangeCheck() && showOnlyUnmatching)
            showOnlyMatching = false;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical(); // 结束匹配设置的垂直布局


        // 源物体和目标物体选择
        EditorGUILayout.Space();

        // 开始水平布局容器 - 使用ExpandWidth让所有元素填充可用空间
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

        // 源物体选择
        EditorGUILayout.BeginVertical("Box", GUILayout.ExpandWidth(true));
        EditorGUI.BeginChangeCheck();
        sourceObject = (GameObject)EditorGUILayout.ObjectField("材质来源物体", sourceObject, typeof(GameObject), true, GUILayout.ExpandWidth(true));
        if (EditorGUI.EndChangeCheck() && sourceObject != null)
        {
            ScanRenderers(sourceObject, sourceRenderers);
            UpdateMatches();
        }
        EditorGUILayout.EndVertical();

        // 添加箭头 - 自适应宽度
        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);

        // 使用GUIContent计算文本宽度
        GUIContent arrowContent = new GUIContent("→将复制到>>>");
        GUIStyle arrowStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(5, 5, 0, 0) // 添加左右内边距
        };

        // 自适应宽度标签
        EditorGUILayout.LabelField(arrowContent, arrowStyle);
        EditorGUILayout.EndVertical();

        // 目标物体选择
        EditorGUILayout.BeginVertical("Box", GUILayout.ExpandWidth(true));
        EditorGUI.BeginChangeCheck();
        targetObject = (GameObject)EditorGUILayout.ObjectField("目标物体", targetObject, typeof(GameObject), true, GUILayout.ExpandWidth(true));

        // 将目标物体的变化检查移到内部
        if (EditorGUI.EndChangeCheck() && targetObject != null)
        {
            ScanRenderers(targetObject, targetRenderers);
            UpdateMatches();
        }
        EditorGUILayout.EndVertical();

        // 结束外层水平布局容器
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (sourceObject == null || targetObject == null)
        {
            EditorGUILayout.HelpBox("请先拖入源物体和目标物体", MessageType.Info);
            return;
        }

        // 显示扫描结果
        GUILayout.BeginHorizontal();

        // 源物体网格列表
        GUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 5));
        GUILayout.Label($"源物体网格 ({sourceRenderers.Count})", EditorStyles.boldLabel);
        sourceScrollPos = GUILayout.BeginScrollView(sourceScrollPos, "box");

        if (sourceRenderers.Count == 0)
        {
            GUILayout.Label("未找到网格渲染器");
        }
        else
        {
            foreach (var rendererInfo in sourceRenderers)
            {
                if (showOnlyMatching && !rendererInfo.hasMatch) continue;
                if (showOnlyUnmatching && rendererInfo.hasMatch) continue;
                RenderRendererInfo(rendererInfo, true);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // 目标物体网格列表
        GUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 5));
        GUILayout.Label($"目标物体网格 ({targetRenderers.Count})", EditorStyles.boldLabel);
        targetScrollPos = GUILayout.BeginScrollView(targetScrollPos, "box");

        if (targetRenderers.Count == 0)
        {
            GUILayout.Label("未找到网格渲染器");
        }
        else
        {
            foreach (var rendererInfo in targetRenderers)
            {
                if (showOnlyMatching && !rendererInfo.hasMatch) continue;
                if (showOnlyUnmatching && rendererInfo.hasMatch) continue;
                RenderRendererInfo(rendererInfo, false);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // 操作按钮区域
        EditorGUILayout.BeginVertical("Box");
        GUILayout.Label("操作按钮", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "重新刷新匹配：重新扫描物体并尝试匹配所有渲染器\n" +
            "尝试自动匹配：使用材质信息对未匹配项进行智能匹配（绿色标记）",
            MessageType.Info);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("重新刷新匹配", GUILayout.Height(30)))
        {
            ScanRenderers(sourceObject, sourceRenderers);
            ScanRenderers(targetObject, targetRenderers);
            UpdateMatches();
        }

        GUI.enabled = targetRenderers.Any(r => !r.hasMatch);
        if (GUILayout.Button("尝试自动匹配", GUILayout.Height(30)))
        {
            EnhancedAutoMatch();
        }
        GUI.enabled = true;

        GUI.enabled = sourceRenderers.Any(r => r.selected && r.hasMatch);
        if (GUILayout.Button("复制选定材质", GUILayout.Height(30)))
        {
            CopyMaterials();
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // 状态信息
        int selectedCount = sourceRenderers.Count(r => r.selected && r.hasMatch);
        int unmatchedCount = targetRenderers.Count(r => !r.hasMatch);
        int autoMatchedCount = targetRenderers.Count(r => r.isAutoMatched);
        GUILayout.Label($"已选择 {selectedCount} 个渲染器 | 未匹配项: {unmatchedCount} | 自动匹配: {autoMatchedCount}", EditorStyles.boldLabel);
    }

    // ==================== 原有 MaterialVariant 模式渲染 ====================
    private void RenderMaterialVariantMode()
    {
        // 目标物体选择
        EditorGUILayout.BeginVertical("Box");
        EditorGUI.BeginChangeCheck();
        targetObject = (GameObject)EditorGUILayout.ObjectField("目标物体", targetObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && targetObject != null)
        {
            ScanRenderers(targetObject, targetRenderers);
            if (materialFolder != null)
            {
                ScanVariantMaterials();
                MatchMaterials();
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 材质文件夹选择
        EditorGUILayout.BeginVertical("Box");
        EditorGUI.BeginChangeCheck();
        materialFolder = (DefaultAsset)EditorGUILayout.ObjectField("材质文件夹", materialFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck() && materialFolder != null)
        {
            ScanVariantMaterials();
            MatchMaterials();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 选项
        EditorGUILayout.BeginVertical("Box");
        showMaterialPreview = EditorGUILayout.ToggleLeft("显示材质预览", showMaterialPreview);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        if (targetObject == null)
        {
            EditorGUILayout.HelpBox("请先拖入目标物体", MessageType.Info);
            return;
        }

        if (materialFolder == null)
        {
            EditorGUILayout.HelpBox("请拖入包含变体材质（其它颜色）的文件夹", MessageType.Info);
        }

        // 显示目标物体信息
        EditorGUILayout.LabelField($"目标物体: {targetObject.name}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"找到 {targetRenderers.Count} 个渲染器", EditorStyles.miniLabel);

        if (variantMaterials.Count > 0)
        {
            EditorGUILayout.LabelField($"找到 {variantMaterials.Count} 个变体材质", EditorStyles.miniLabel);
        }

        scrollPos = GUILayout.BeginScrollView(scrollPos, "box");

        // 显示渲染器和材质信息
        if (targetRenderers.Count == 0)
        {
            GUILayout.Label("未找到网格渲染器");
        }
        else
        {
            foreach (var rendererInfo in targetRenderers)
            {
                RenderVariantRendererInfo(rendererInfo);
            }
        }

        GUILayout.EndScrollView();

        EditorGUILayout.Space();

        // 操作按钮
        EditorGUILayout.BeginVertical("Box");
        GUILayout.Label("操作", EditorStyles.boldLabel);

        GUI.enabled = targetRenderers.Count > 0 && variantMaterials.Count > 0;
        if (GUILayout.Button("应用变体材质", GUILayout.Height(30)))
        {
            ApplyVariantMaterials();
        }
        GUI.enabled = true;

        EditorGUILayout.EndVertical();
    }

    // ==================== 新增 LilToon 批量创建模式渲染 ====================
    private void RenderLilToonBatchCreatorMode()
    {
        // 设置窗口最小尺寸
        minSize = new Vector2(400, 600);

        // 窗口标题栏
        GUILayout.Label("LilToon批量材质创建工具", EditorStyles.boldLabel);
        EditorGUILayout.Separator();

        // 每次绘制UI都重置提示 + 实时检测模板已用槽位
        errorMessage = "";
        successMessage = "";
        UpdateTemplateUsedChannels();

        // 核心UI绘制（按功能模块分区，更易操作）
        DrawBatchFolderUI();
        EditorGUILayout.Separator();
        DrawTemplateMaterialUI();
        EditorGUILayout.Separator();
        DrawFixedTextureUI();
        EditorGUILayout.Separator();
        DrawChannelSelectUI();
        EditorGUILayout.Separator();
        DrawBlendModeUI();
        EditorGUILayout.Separator();
        DrawOutputPathUI();
        EditorGUILayout.Separator();
        DrawCreateButton();

        // 提示信息区域（红色错误/绿色成功）
        if (!string.IsNullOrEmpty(errorMessage))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
        }
        if (!string.IsNullOrEmpty(successMessage))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(successMessage, MessageType.Info);
        }

        // 实时更新可用槽位（模板/固定图片状态变化时自动过滤）
        UpdateFixedAvailableChannels();
        UpdateBatchAvailableChannels();
    }

    #region LilToon 功能模块UI绘制（分区展示，逻辑清晰）
    /// <summary>批量图片文件夹选择UI</summary>
    private void DrawBatchFolderUI()
    {
        GUILayout.Label("📁 批量图片文件夹", EditorStyles.label);
        batchImageFolder = EditorGUILayout.ObjectField("图片文件夹", batchImageFolder, typeof(UnityEngine.Object), false);
        EditorGUILayout.HelpBox("拖入包含待处理纹理的Unity文件夹，工具会遍历所有Texture2D", MessageType.None);
    }

    /// <summary>模板材质配置UI（克隆属性/已用槽位检测/覆盖开关）</summary>
    private void DrawTemplateMaterialUI()
    {
        GUILayout.Label("🎨 模板材质配置", EditorStyles.label);
        enableTemplateMaterial = EditorGUILayout.Toggle("启用模板材质", enableTemplateMaterial);
        if (enableTemplateMaterial)
        {
            EditorGUI.indentLevel++;
            templateMaterial = (Material)EditorGUILayout.ObjectField("LilToon模板材质", templateMaterial, typeof(Material), false);
            // 实时显示模板已用槽位，方便用户查看
            if (templateUsedChannels.Count > 0)
            {
                string usedChannelTips = "模板已用主色槽位：" + string.Join("、", templateUsedChannels.Select(i => _mainChannelNames[i]));
                EditorGUILayout.HelpBox(usedChannelTips, MessageType.None);
            }
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>固定图片配置UI（绑定槽位/模板覆盖开关）</summary>
    private void DrawFixedTextureUI()
    {
        GUILayout.Label("🖼️ 固定图片配置（全局绑定）", EditorStyles.label);
        enableFixedTexture = EditorGUILayout.Toggle("启用固定图片", enableFixedTexture);
        if (enableFixedTexture)
        {
            EditorGUI.indentLevel++;
            fixedTexture = (Texture2D)EditorGUILayout.ObjectField("固定纹理", fixedTexture, typeof(Texture2D), false);
            // 仅启用模板时显示【覆盖模板槽位】开关
            if (enableTemplateMaterial)
            {
                enableCoverTemplateChannel = EditorGUILayout.Toggle("允许覆盖模板已用槽位", enableCoverTemplateChannel);
            }
            // 动态显示可用的固定图片槽位（过滤模板已用/覆盖开关控制）
            if (fixedAvailableChannelNames.Count > 0)
            {
                fixedChannelSelect = EditorGUILayout.Popup("固定图片绑定槽位", fixedChannelSelect, fixedAvailableChannelNames.ToArray());
            }
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>槽位选择UI（批量图片绑定槽位，自动过滤已占用）</summary>
    private void DrawChannelSelectUI()
    {
        GUILayout.Label("🔧 槽位选择配置", EditorStyles.label);
        if (batchAvailableChannelNames.Count > 0)
        {
            batchChannelSelect = EditorGUILayout.Popup("批量图片绑定槽位", batchChannelSelect, batchAvailableChannelNames.ToArray());
        }
        EditorGUILayout.HelpBox("可用槽位 = 所有槽位 - 模板已用槽位（未覆盖） - 固定图片槽位", MessageType.None);
    }

    /// <summary>混合模式配置UI（主色2/3的混合模式）</summary>
    private void DrawBlendModeUI()
    {
        GUILayout.Label("⚙️ 混合模式配置", EditorStyles.label);
        blendMode2nd = EditorGUILayout.Popup("主色2 混合模式", blendMode2nd, _blendModeNames);
        blendMode3rd = EditorGUILayout.Popup("主色3 混合模式", blendMode3rd, _blendModeNames);
    }

    /// <summary>输出路径配置UI</summary>
    private void DrawOutputPathUI()
    {
        GUILayout.Label("📌 输出路径配置", EditorStyles.label);
        outputPath = EditorGUILayout.TextField("材质保存路径", outputPath);
        if (GUILayout.Button("打开输出文件夹", GUILayout.Width(120)))
        {
            // 打开文件夹，不存在则提示
            if (Directory.Exists(outputPath)) Application.OpenURL("file:///" + outputPath);
            else errorMessage = "输出文件夹尚未创建，创建材质后会自动生成！";
        }
    }

    /// <summary>批量创建按钮UI</summary>
    private void DrawCreateButton()
    {
        EditorGUILayout.Space(10);
        // 按钮按窗口宽度拉伸，突出显示
        if (GUILayout.Button("开始批量创建材质", GUILayout.Height(40)))
        {
            CreateBatchMaterials();
        }
    }
    #endregion

    #region LilToon 核心业务逻辑 - 批量创建材质
    private void CreateBatchMaterials()
    {
        // 修复核心：全局唯一声明，全方法复用
        int actualFixedChannel = -1;

        // 1. 基础校验：批量文件夹是否有效
        if (batchImageFolder == null)
        {
            errorMessage = "请先选择有效的批量图片文件夹！";
            return;
        }
        string folderPath = AssetDatabase.GetAssetPath(batchImageFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            errorMessage = "拖入的不是有效的Unity文件夹，请重新选择！";
            return;
        }

        // 2. 模板材质校验：启用则必须是LilToon材质
        if (enableTemplateMaterial)
        {
            if (templateMaterial == null)
            {
                errorMessage = "启用了模板材质，请拖入有效的LilToon材质！";
                return;
            }
            if (!templateMaterial.shader.name.Contains(LIL_TOON_SHADER_KEY))
            {
                errorMessage = "模板材质不是LilToon着色器，仅支持LilToon材质作为模板！";
                return;
            }
        }

        // 3. 固定图片校验：启用则必须选择纹理+有效槽位
        if (enableFixedTexture)
        {
            if (fixedTexture == null)
            {
                errorMessage = "启用了固定图片，请选择有效的2D纹理！";
                return;
            }
            actualFixedChannel = GetActualFixedChannel();
            if (actualFixedChannel < 0)
            {
                errorMessage = "固定图片无可用绑定槽位，请检查模板槽位/覆盖开关！";
                return;
            }
        }

        // 4. 批量图片槽位校验：是否有可用槽位
        if (batchAvailableChannelIndices.Count == 0)
        {
            errorMessage = "批量图片无可用绑定槽位（模板+固定图片已占用所有槽位）！";
            return;
        }
        int actualBatchChannel = GetActualBatchChannel();
        if (actualBatchChannel < 0)
        {
            errorMessage = "请选择有效的批量图片绑定槽位！";
            return;
        }

        // 5. 着色器校验：未启用模板时，必须找到LilToon着色器
        Shader lilToonShader = FindLilToonShader();
        if (lilToonShader == null && !enableTemplateMaterial)
        {
            errorMessage = "未找到LilToon着色器，请先导入LilToon插件！";
            return;
        }

        // 6. 遍历文件夹内所有Texture2D（过滤非图片文件）
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        if (textureGuids.Length == 0)
        {
            errorMessage = $"所选文件夹【{folderPath}】内未找到任何2D纹理！";
            return;
        }
        var textureList = textureGuids
            .Select(guid => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(tex => tex != null) // 过滤空纹理
            .ToList();

        // 7. 自动创建输出路径（多级路径支持）
        if (!AssetDatabase.IsValidFolder(outputPath))
        {
            CreateUnityFolderRecursively(outputPath);
        }

        // 8. 循环创建材质（克隆模板/新建 + 赋值纹理）
        int createSuccessCount = 0;
        string batchFolderName = Path.GetFileName(folderPath);
        string finalOutputPath = $"{outputPath}/{batchFolderName}";
        // 确保最终输出路径存在
        if (!AssetDatabase.IsValidFolder(finalOutputPath)) CreateUnityFolderRecursively(finalOutputPath);

        foreach (var batchTex in textureList)
        {
            try
            {
                // 唯一修复处：加UnityEngine.限定Object，解决歧义
                Material newMaterial = enableTemplateMaterial
                    ? UnityEngine.Object.Instantiate(templateMaterial)
                    : new Material(lilToonShader);

                // 赋值固定图片（启用则执行，覆盖开关控制是否替换模板槽位）
                if (enableFixedTexture && fixedTexture != null && actualFixedChannel >= 0)
                {
                    SetMaterialChannelTexture(newMaterial, fixedTexture, actualFixedChannel);
                }

                // 赋值批量图片（核心批量逻辑）
                SetMaterialChannelTexture(newMaterial, batchTex, actualBatchChannel);

                // 材质命名（含模块标识，便于识别）
                string materialName = GetMaterialName(batchTex.name, actualFixedChannel, actualBatchChannel);
                // 生成唯一路径，避免覆盖已有材质
                string uniqueMatPath = AssetDatabase.GenerateUniqueAssetPath($"{finalOutputPath}/{materialName}.mat");
                // 保存材质到工程
                AssetDatabase.CreateAsset(newMaterial, uniqueMatPath);
                createSuccessCount++;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"处理纹理{batchTex.name}失败：{ex.Message}");
                continue;
            }
        }

        // 9. 刷新资源库 + 显示完成提示
        AssetDatabase.Refresh();
        successMessage = $"批量创建完成！\n共处理{textureList.Count}张纹理，成功创建{createSuccessCount}个材质！\n保存路径：{finalOutputPath}";
        // 自动选中输出文件夹，方便用户查看
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(finalOutputPath);
    }
    #endregion

    #region LilToon 核心工具方法 - 槽位检测/过滤/纹理赋值
    /// <summary>实时检测模板材质的已用主色槽位</summary>
    private void UpdateTemplateUsedChannels()
    {
        templateUsedChannels.Clear();
        if (!enableTemplateMaterial || templateMaterial == null) return;

        // 遍历3个主色槽位，检测是否绑定了纹理
        for (int i = 0; i < _mainChannelTexProps.Length; i++)
        {
            if (templateMaterial.GetTexture(_mainChannelTexProps[i]) != null)
            {
                templateUsedChannels.Add(i);
            }
        }
    }

    /// <summary>更新固定图片的可用槽位（根据模板/覆盖开关过滤）</summary>
    private void UpdateFixedAvailableChannels()
    {
        fixedAvailableChannelIndices.Clear();
        fixedAvailableChannelNames.Clear();

        for (int i = 0; i < _mainChannelNames.Length; i++)
        {
            // 显示规则：模板未启用 / 勾选覆盖 / 模板未用该槽位 → 显示
            bool isChannelAvailable = !enableTemplateMaterial || enableCoverTemplateChannel || !templateUsedChannels.Contains(i);
            if (isChannelAvailable)
            {
                fixedAvailableChannelIndices.Add(i);
                fixedAvailableChannelNames.Add(_mainChannelNames[i]);
            }
        }

        // 防止选择索引越界
        if (fixedChannelSelect >= fixedAvailableChannelIndices.Count)
        {
            fixedChannelSelect = 0;
        }
    }

    /// <summary>更新批量图片的可用槽位（过滤：模板已用(未覆盖) + 固定图片槽位）</summary>
    private void UpdateBatchAvailableChannels()
    {
        batchAvailableChannelIndices.Clear();
        batchAvailableChannelNames.Clear();
        int actualFixedChan = GetActualFixedChannel();

        for (int i = 0; i < _mainChannelNames.Length; i++)
        {
            // 过滤规则1：模板启用+未勾选覆盖+模板已用 → 过滤
            bool isTemplateUsed = enableTemplateMaterial && !enableCoverTemplateChannel && templateUsedChannels.Contains(i);
            // 过滤规则2：固定图片启用+已绑定该槽位 → 过滤
            bool isFixedUsed = enableFixedTexture && actualFixedChan >= 0 && i == actualFixedChan;

            if (!isTemplateUsed && !isFixedUsed)
            {
                batchAvailableChannelIndices.Add(i);
                batchAvailableChannelNames.Add(_mainChannelNames[i]);
            }
        }

        // 防止选择索引越界
        if (batchChannelSelect >= batchAvailableChannelIndices.Count)
        {
            batchChannelSelect = 0;
        }
    }

    /// <summary>获取固定图片的实际绑定槽位索引</summary>
    private int GetActualFixedChannel()
    {
        return fixedAvailableChannelIndices.Count > 0 && fixedChannelSelect < fixedAvailableChannelIndices.Count
            ? fixedAvailableChannelIndices[fixedChannelSelect]
            : -1;
    }

    /// <summary>获取批量图片的实际绑定槽位索引</summary>
    private int GetActualBatchChannel()
    {
        return batchAvailableChannelIndices.Count > 0 && batchChannelSelect < batchAvailableChannelIndices.Count
            ? batchAvailableChannelIndices[batchChannelSelect]
            : -1;
    }

    /// <summary>给材质指定槽位赋值纹理+混合模式（支持覆盖原有纹理）</summary>
    private void SetMaterialChannelTexture(Material mat, Texture2D tex, int channelIndex)
    {
        if (mat == null || tex == null || channelIndex < 0 || channelIndex >= 3) return;

        switch (channelIndex)
        {
            case 0: // 主色1 - 仅赋值纹理
                mat.SetTexture(_mainChannelTexProps[0], tex);
                break;
            case 1: // 主色2 - 纹理+混合模式+启用开关
                mat.SetTexture(_mainChannelTexProps[1], tex);
                mat.SetFloat("_UseMain2ndTex", 1.0f);
                mat.SetColor("_Color2", Color.white);
                mat.SetFloat("_Main2ndTexBlendMode", GetBlendModeValue(blendMode2nd));
                mat.EnableKeyword("_MAIN2NDTEX_ON");
                break;
            case 2: // 主色3 - 纹理+混合模式+启用开关
                mat.SetTexture(_mainChannelTexProps[2], tex);
                mat.SetFloat("_UseMain3rdTex", 1.0f);
                mat.SetColor("_Color3", Color.white);
                mat.SetFloat("_Main3rdTexBlendMode", GetBlendModeValue(blendMode3rd));
                mat.EnableKeyword("_MAIN3RDTEX_ON");
                break;
        }
    }

    /// <summary>查找工程中的LilToon着色器（兼容UPM/本地导入）</summary>
    private Shader FindLilToonShader()
    {
        // 优先查找标准路径
        Shader shader = Shader.Find($"{LIL_TOON_SHADER_KEY}/{LIL_TOON_SHADER_KEY}");
        if (shader != null) return shader;

        // 遍历工程所有着色器，查找LilToon
        foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
        {
            string shaderPath = AssetDatabase.GUIDToAssetPath(guid);
            if (shaderPath.Contains(LIL_TOON_SHADER_KEY) && Path.GetFileNameWithoutExtension(shaderPath) == LIL_TOON_SHADER_KEY)
            {
                return AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            }
        }

        // 最后尝试查找隐藏路径
        return Shader.Find(LIL_TOON_SHADER_KEY);
    }
    #endregion

    #region LilToon 辅助工具方法 - 命名/文件夹/混合模式
    /// <summary>生成材质名称（含模板/固定/覆盖标识，便于识别）</summary>
    private string GetMaterialName(string texName, int fixedChan, int batchChan)
    {
        string templateTag = enableTemplateMaterial ? "Tpl_" : "";
        string fixedTag = enableFixedTexture ? $"Fix{GetChannelShortName(fixedChan)}_" : "";
        string coverTag = enableCoverTemplateChannel ? "Cover_" : "";
        return $"{coverTag}{templateTag}{fixedTag}{texName}_Batch{GetChannelShortName(batchChan)}";
    }

    /// <summary>槽位短名称（Main1/Main2/Main3）</summary>
    private string GetChannelShortName(int channelIndex)
    {
        return channelIndex switch
        {
            0 => "Main1",
            1 => "Main2",
            2 => "Main3",
            _ => "Unknown"
        };
    }

    /// <summary>混合模式索引转数值（适配LilToon）</summary>
    private float GetBlendModeValue(int modeIndex)
    {
        return modeIndex switch
        {
            0 => 0f,
            1 => 1f,
            2 => 2f,
            3 => 3f,
            _ => 0f
        };
    }

    /// <summary>递归创建Unity工程文件夹（支持多级路径，如A/B/C）</summary>
    private void CreateUnityFolderRecursively(string fullPath)
    {
        if (AssetDatabase.IsValidFolder(fullPath)) return;

        string[] pathParts = fullPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        string currentPath = pathParts[0];

        for (int i = 1; i < pathParts.Length; i++)
        {
            string nextPath = $"{currentPath}/{pathParts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, pathParts[i]);
            }
            currentPath = nextPath;
        }
        AssetDatabase.Refresh();
    }
    #endregion

    // ==================== 原有 MaterialTransferTool 辅助方法 ====================
    private void RenderVariantRendererInfo(RendererInfo info)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 渲染器名称
        EditorGUILayout.LabelField(info.name, EditorStyles.boldLabel);

        // 显示相对路径
        if (!string.IsNullOrEmpty(info.relativePath))
        {
            EditorGUILayout.LabelField(info.relativePath, pathStyle);
        }

        // 材质槽列表 - 紧凑无间距左右对比布局
        if (showMaterialPreview && info.materials.Length > 0)
        {
            // 表头
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("原始材质", GUILayout.Width(position.width / 2 - 5));
            EditorGUILayout.LabelField("→", GUILayout.Width(10));
            EditorGUILayout.LabelField("变体材质", GUILayout.Width(position.width / 2 - 5));
            EditorGUILayout.EndHorizontal();

            // 材质槽位
            for (int i = 0; i < info.materials.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // 左侧：原始材质
                EditorGUILayout.BeginHorizontal(GUILayout.Width(position.width / 2 - 5));
                EditorGUILayout.LabelField($"槽位 {i}:", GUILayout.Width(45));
                EditorGUILayout.ObjectField(info.materials[i], typeof(Material), false, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                // 中间：箭头
                EditorGUILayout.LabelField("→", centeredLabelStyle, GUILayout.Width(10));

                // 右侧：变体材质
                EditorGUILayout.BeginHorizontal(GUILayout.Width(position.width / 2 - 5));
                if (info.matchedVariants != null && i < info.matchedVariants.Count && info.matchedVariants[i] != null)
                {
                    EditorGUILayout.ObjectField(info.matchedVariants[i], typeof(Material), false, GUILayout.ExpandWidth(true));
                }
                else
                {
                    EditorGUILayout.LabelField("无匹配", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndHorizontal();
            }
        }
        else if (!showMaterialPreview)
        {
            EditorGUILayout.LabelField($"{info.materials.Length} 个材质槽位", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void RenderRendererInfo(RendererInfo info, bool isSource)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 渲染器名称和选择状态
        EditorGUILayout.BeginHorizontal();
        info.selected = EditorGUILayout.Toggle(info.selected, GUILayout.Width(20));

        // 显示匹配状态
        if (!isSource)
        {
            if (info.isAutoMatched)
            {
                // 自动匹配项使用绿色标记
                GUILayout.Label("✓ 自动匹配", autoMatchedStyle, GUILayout.Width(80));
            }
            else if (!info.hasMatch)
            {
                GUILayout.Label("⚠ 未匹配", warningStyle, GUILayout.Width(60));
            }
            else
            {
                GUILayout.Label("✓ 已匹配", EditorStyles.miniLabel, GUILayout.Width(60));
            }
        }

        EditorGUILayout.LabelField(info.name, EditorStyles.boldLabel);

        // 显示关联的对象
        if (info.matchedRenderer != null && !isSource)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"← {info.matchedRenderer.name}", pathStyle);
        }

        EditorGUILayout.EndHorizontal();

        // 显示相对路径
        if (useRelativePath && !string.IsNullOrEmpty(info.relativePath))
        {
            EditorGUILayout.LabelField(info.relativePath, pathStyle);
        }

        // 材质槽列表
        if (showMaterialPreview)
        {
            for (int i = 0; i < info.materials.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(25);
                EditorGUILayout.LabelField($"槽位 {i}:", GUILayout.Width(50));
                EditorGUILayout.ObjectField(info.materials[i], typeof(Material), false);
                EditorGUILayout.EndHorizontal();
            }
        }

        // 匹配选择框（仅目标物体显示）
        if (!isSource)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(25);

            // 下拉菜单选择源物体匹配
            int selectedIndex = sourceRenderers.IndexOf(info.matchedRendererInfo);
            if (selectedIndex < 0) selectedIndex = 0;

            EditorGUI.BeginChangeCheck();

            // 创建渲染器名称列表（带路径）
            string[] sourceNames = sourceRenderers.Select(r =>
                $"{r.name} ({r.relativePath})").ToArray();

            int newIndex = EditorGUILayout.Popup("匹配源:", selectedIndex, sourceNames);

            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < sourceRenderers.Count)
            {
                info.matchedRendererInfo = sourceRenderers[newIndex];
                info.matchedRenderer = sourceRenderers[newIndex].renderer;
                info.hasMatch = true;
                info.isAutoMatched = false; // 手动选择时重置自动匹配标记
                sourceRenderers[newIndex].hasMatch = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void ScanRenderers(GameObject root, List<RendererInfo> list)
    {
        list.Clear();
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            var info = new RendererInfo
            {
                renderer = renderer,
                name = renderer.gameObject.name,
                materials = renderer.sharedMaterials,
                relativePath = GetRelativePath(renderer.gameObject, root),
                selected = true,
                hasMatch = false,
                isAutoMatched = false // 初始化自动匹配标记
            };

            // 初始化匹配列表（用于变体模式）
            info.matchedVariants = new List<Material>();
            for (int i = 0; i < info.materials.Length; i++)
            {
                info.matchedVariants.Add(null);
            }

            list.Add(info);
        }
    }

    private void ScanVariantMaterials()
    {
        variantMaterials.Clear();
        if (materialFolder == null) return;

        string folderPath = AssetDatabase.GetAssetPath(materialFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError("请选择有效的文件夹");
            return;
        }

        // 获取文件夹中所有材质
        string[] materialPaths = Directory.GetFiles(folderPath, "*.mat", SearchOption.AllDirectories);
        foreach (string path in materialPaths)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                variantMaterials.Add(material);
            }
        }

        Debug.Log($"找到 {variantMaterials.Count} 个材质");
    }

    private void MatchMaterials()
    {
        // 为每个渲染器的每个材质槽位查找匹配的变体
        foreach (var rendererInfo in targetRenderers)
        {
            for (int i = 0; i < rendererInfo.materials.Length; i++)
            {
                Material originalMat = rendererInfo.materials[i];
                if (originalMat == null) continue;

                string baseName = GetBaseMaterialName(originalMat.name);

                // 查找匹配的变体材质
                Material matchedVariant = variantMaterials.FirstOrDefault(m =>
                    m != null && GetBaseMaterialName(m.name) == baseName && m != originalMat);

                if (matchedVariant != null)
                {
                    rendererInfo.matchedVariants[i] = matchedVariant;
                }
                else
                {
                    rendererInfo.matchedVariants[i] = null;
                }
            }
        }
    }

    private string GetBaseMaterialName(string materialName)
    {
        // 尝试从材质名称中提取基础名称
        // 例如：AA_Red -> AA, AA_Yellow -> AA
        // 这里使用简单的下划线分割，可以根据需要调整逻辑

        int lastUnderscore = materialName.LastIndexOf('_');
        if (lastUnderscore > 0)
        {
            return materialName.Substring(0, lastUnderscore);
        }

        return materialName;
    }

    private string GetRelativePath(GameObject obj, GameObject root)
    {
        if (obj == root) return "(Root)";

        List<string> path = new List<string>();
        Transform current = obj.transform;

        while (current != null && current.gameObject != root)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }

        return string.Join("/", path);
    }

    private void UpdateMatches()
    {
        // 重置所有匹配状态
        foreach (var info in targetRenderers)
        {
            info.matchedRenderer = null;
            info.matchedRendererInfo = null;
            info.hasMatch = false;
            info.isAutoMatched = false; // 重置自动匹配标记
        }

        foreach (var info in sourceRenderers)
        {
            info.hasMatch = false;
        }

        // 尝试匹配
        foreach (var targetInfo in targetRenderers)
        {
            RendererInfo bestMatch = FindBestMatch(targetInfo, sourceRenderers);

            if (bestMatch != null)
            {
                targetInfo.matchedRenderer = bestMatch.renderer;
                targetInfo.matchedRendererInfo = bestMatch;
                targetInfo.hasMatch = true;
                bestMatch.hasMatch = true;
            }
        }
    }

    private RendererInfo FindBestMatch(RendererInfo targetInfo, List<RendererInfo> sourceInfos)
    {
        // 尝试精确匹配
        foreach (var sourceInfo in sourceInfos)
        {
            bool nameMatch = matchByName && sourceInfo.name == targetInfo.name;
            bool pathMatch = useRelativePath && matchByHierarchy &&
                             sourceInfo.relativePath == targetInfo.relativePath;

            if (nameMatch || pathMatch)
            {
                return sourceInfo;
            }
        }

        // 尝试部分匹配
        foreach (var sourceInfo in sourceInfos)
        {
            bool nameContains = matchByName &&
                (sourceInfo.name.Contains(targetInfo.name) || targetInfo.name.Contains(sourceInfo.name));

            bool pathContains = useRelativePath && matchByHierarchy &&
                (sourceInfo.relativePath.Contains(targetInfo.relativePath) ||
                 targetInfo.relativePath.Contains(sourceInfo.relativePath));

            if (nameContains || pathContains)
            {
                return sourceInfo;
            }
        }

        return null;
    }

    /// <summary>
    /// 增强的自动匹配算法（考虑材质槽位数量和材质名称）
    /// </summary>
    private void EnhancedAutoMatch()
    {
        int matchedCount = 0;

        // 获取所有未匹配的目标渲染器
        var unmatchedTargets = targetRenderers.Where(t => !t.hasMatch).ToList();
        // 获取所有未匹配的源渲染器
        var availableSources = sourceRenderers.Where(s => !s.hasMatch).ToList();

        // 第一优先级：材质槽位数量和材质名称完全匹配
        foreach (var targetInfo in unmatchedTargets.ToList())
        {
            // 查找材质槽位数量相同且所有材质名称都匹配的源
            var perfectMatch = availableSources.FirstOrDefault(s =>
                s.materials.Length == targetInfo.materials.Length &&
                MaterialsMatchExactly(s.materials, targetInfo.materials));

            if (perfectMatch != null)
            {
                MatchPair(targetInfo, perfectMatch, true);
                unmatchedTargets.Remove(targetInfo);
                availableSources.Remove(perfectMatch);
                matchedCount++;
                continue;
            }
        }

        // 第二优先级：材质槽位数量相同且部分材质名称匹配
        foreach (var targetInfo in unmatchedTargets.ToList())
        {
            var bestMatch = availableSources
                .Where(s => s.materials.Length == targetInfo.materials.Length)
                .OrderByDescending(s => CountMatchingMaterials(s.materials, targetInfo.materials))
                .FirstOrDefault();

            if (bestMatch != null && CountMatchingMaterials(bestMatch.materials, targetInfo.materials) > 0)
            {
                MatchPair(targetInfo, bestMatch, true);
                unmatchedTargets.Remove(targetInfo);
                availableSources.Remove(bestMatch);
                matchedCount++;
            }
        }

        // 第三优先级：材质槽位数量相同（不要求材质名称匹配）
        foreach (var targetInfo in unmatchedTargets.ToList())
        {
            var slotMatch = availableSources
                .FirstOrDefault(s => s.materials.Length == targetInfo.materials.Length);

            if (slotMatch != null)
            {
                MatchPair(targetInfo, slotMatch, true);
                unmatchedTargets.Remove(targetInfo);
                availableSources.Remove(slotMatch);
                matchedCount++;
            }
        }

        // 第四优先级：使用原始匹配方法作为后备
        foreach (var targetInfo in unmatchedTargets.ToList())
        {
            var fallbackMatch = FindBestMatch(targetInfo, availableSources);
            if (fallbackMatch != null)
            {
                MatchPair(targetInfo, fallbackMatch, true);
                availableSources.Remove(fallbackMatch);
                matchedCount++;
            }
        }

        Debug.Log($"尝试自动匹配了 {matchedCount} 个渲染器");
    }

    /// <summary>
    /// 检查两个材质数组是否完全匹配
    /// </summary>
    private bool MaterialsMatchExactly(Material[] a, Material[] b)
    {
        if (a.Length != b.Length) return false;

        for (int i = 0; i < a.Length; i++)
        {
            string nameA = a[i] != null ? a[i].name : "";
            string nameB = b[i] != null ? b[i].name : "";

            if (!nameA.Equals(nameB, System.StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 计算两个材质数组的匹配材质数量
    /// </summary>
    private int CountMatchingMaterials(Material[] a, Material[] b)
    {
        int matchCount = 0;
        int minLength = Mathf.Min(a.Length, b.Length);

        for (int i = 0; i < minLength; i++)
        {
            string nameA = a[i] != null ? a[i].name : "";
            string nameB = b[i] != null ? b[i].name : "";

            if (nameA.Equals(nameB, System.StringComparison.OrdinalIgnoreCase))
                matchCount++;
        }

        return matchCount;
    }

    /// <summary>
    /// 将目标与源匹配
    /// </summary>
    private void MatchPair(RendererInfo target, RendererInfo source, bool isAutoMatch = false)
    {
        target.matchedRenderer = source.renderer;
        target.matchedRendererInfo = source;
        target.hasMatch = true;
        target.isAutoMatched = isAutoMatch; // 设置自动匹配标记
        source.hasMatch = true;
    }

    private void CopyMaterials()
    {
        if (sourceObject == null || targetObject == null) return;

        int copiedCount = 0;

        // 复制材质
        foreach (var targetInfo in targetRenderers)
        {
            if (!targetInfo.selected || !targetInfo.hasMatch || targetInfo.matchedRendererInfo == null)
                continue;

            var sourceInfo = targetInfo.matchedRendererInfo;
            var targetRenderer = targetInfo.renderer;

            // 记录Undo操作，确保可以被撤销和动画器记录
            Undo.RecordObject(targetRenderer, "Copy Materials");

            // 复制材质（只复制目标渲染器已有的槽位）
            Material[] newMaterials = new Material[targetRenderer.sharedMaterials.Length];
            targetRenderer.sharedMaterials.CopyTo(newMaterials, 0);

            int maxSlot = Mathf.Min(sourceInfo.materials.Length, newMaterials.Length);
            for (int i = 0; i < maxSlot; i++)
            {
                newMaterials[i] = sourceInfo.materials[i];
            }

            targetRenderer.sharedMaterials = newMaterials;
            copiedCount++;

            // 标记场景为已修改
            EditorUtility.SetDirty(targetRenderer);
        }

        Debug.Log($"成功复制 {copiedCount} 个渲染器的材质");
    }

    private void ApplyVariantMaterials()
    {
        if (targetObject == null) return;

        int appliedCount = 0;

        foreach (var rendererInfo in targetRenderers)
        {
            var renderer = rendererInfo.renderer;
            if (renderer == null) continue;

            // 记录Undo操作，确保可以被动画器记录
            Undo.RecordObject(renderer, "Apply Variant Materials");

            Material[] newMaterials = renderer.sharedMaterials.Clone() as Material[];
            bool hasChanges = false;

            for (int i = 0; i < newMaterials.Length; i++)
            {
                if (rendererInfo.matchedVariants != null && i < rendererInfo.matchedVariants.Count && rendererInfo.matchedVariants[i] != null)
                {
                    newMaterials[i] = rendererInfo.matchedVariants[i];
                    hasChanges = true;
                    appliedCount++;
                }
            }

            if (hasChanges)
            {
                renderer.sharedMaterials = newMaterials;
                EditorUtility.SetDirty(renderer);
            }
        }

        Debug.Log($"成功应用 {appliedCount} 个变体材质");
    }

    private class RendererInfo
    {
        public Renderer renderer;
        public string name;
        public Material[] materials;
        public bool selected;
        public string relativePath;
        public bool hasMatch;
        public bool isAutoMatched;
        public Renderer matchedRenderer;
        public RendererInfo matchedRendererInfo;
        public List<Material> matchedVariants;
    }
}
#endif
