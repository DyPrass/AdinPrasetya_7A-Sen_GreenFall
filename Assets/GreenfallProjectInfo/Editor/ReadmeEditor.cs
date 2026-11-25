using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.UIElements;

[CustomEditor(typeof(Readme))]
[InitializeOnLoad]
sealed class ReadmeEditor : Editor
{
    static readonly string k_ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";
    static readonly string k_ReadmeSourceDirectory = "Assets/GreenfallProjectInfo";
    static readonly string k_ReadmeAssetPath = "Assets/GreenfallProjectInfo/GreenfallProjectInfo.asset";

    static ReadmeEditor()
        => EditorApplication.delayCall += SelectReadmeAutomatically;

    static void SelectReadmeAutomatically()
    {
        if (!SessionState.GetBool(k_ShowedReadmeSessionStateName, false))
        {
            var readme = SelectReadme();
            SessionState.SetBool(k_ShowedReadmeSessionStateName, true);

            if (readme && !readme.loadedLayout)
            {
                EditorUtility.LoadWindowLayout(Path.Combine(Application.dataPath, "GreenfallProjectInfo/Layout.wlt"));
                readme.loadedLayout = true;
            }
        }
    }

    static Readme SelectReadme()
    {
        var readme = AssetDatabase.LoadAssetAtPath<Readme>(k_ReadmeAssetPath);
        if (readme == null)
        {
            Debug.LogError("Could not load the Greenfall project guide at " + k_ReadmeAssetPath);
            return null;
        }

        Selection.objects = new UnityEngine.Object[] { readme };
        return readme;
    }
    
    void RemoveProjectInfo()
    {
        if (EditorUtility.DisplayDialog("Remove Greenfall Project Guide",
            
            $"All contents under {k_ReadmeSourceDirectory} will be removed, are you sure you want to proceed?",
            "Proceed",
            "Cancel"))
        {
            if (Directory.Exists(k_ReadmeSourceDirectory))
            {
                FileUtil.DeleteFileOrDirectory(k_ReadmeSourceDirectory);
                FileUtil.DeleteFileOrDirectory(k_ReadmeSourceDirectory + ".meta");
            }
            else
            {
                Debug.Log($"Could not find the Readme folder at {k_ReadmeSourceDirectory}");
            }

            var readmeAsset = SelectReadme();
            if (readmeAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(readmeAsset);
                FileUtil.DeleteFileOrDirectory(path + ".meta");
                FileUtil.DeleteFileOrDirectory(path);
            }

            AssetDatabase.Refresh();
        }
    }

    //Remove ImGUI
    protected sealed override void OnHeaderGUI() { }
    public sealed override void OnInspectorGUI() { }

    public override VisualElement CreateInspectorGUI()
    {
        var readme = (Readme)target;

        VisualElement root = new();
        root.styleSheets.Add(readme.commonStyle);
        root.styleSheets.Add(EditorGUIUtility.isProSkin ? readme.darkStyle : readme.lightStyle);

        VisualElement ChainWithClass(VisualElement created, string className)
        {
            created.AddToClassList(className);
            return created;
        }

        //Header
        VisualElement title = new();
        title.AddToClassList("title");
        title.Add(ChainWithClass(new Image() { image = readme.icon }, "title__icon"));
        title.Add(ChainWithClass(new Label(readme.title), "title__text"));
        root.Add(title);

        //Content
        foreach (var section in readme.sections)
        {
            VisualElement part = new();
            part.AddToClassList("section");

            if (!string.IsNullOrEmpty(section.heading))
                part.Add(ChainWithClass(new Label(section.heading), "section__header"));

            if (!string.IsNullOrEmpty(section.text))
                part.Add(ChainWithClass(new Label(section.text), "section__body"));

            if (!string.IsNullOrEmpty(section.linkText))
            {
                var link = ChainWithClass(new Label(section.linkText), "section__link");
                link.RegisterCallback<ClickEvent>(evt => Application.OpenURL(section.url));
                part.Add(link);
            }

            root.Add(part);
        }

        var button = new Button(RemoveProjectInfo) { text = "Remove Greenfall Project Guide" };
        button.AddToClassList("remove-readme-button");
        root.Add(button);

        return root;
    }
}
