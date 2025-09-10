using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class G2CharacterGuildBook : EditorWindow
{
    private const string PlayerPrefsKey = "WelcomePopupShown";
    private bool dontShowAgain = false;
    private GUIContent content;
    private Vector2 scrollPosition;

    private string discordInviteLink = "https://discord.gg/2gGHQnmRs4";
    private string shaderPackageLink = "https://drive.google.com/file/d/1T7w-zwRh9DwcOLt8RjbIUv7Hbx24nBEY/view";
    private string characterSkinProfileLink = "https://drive.google.com/file/d/1-VtxDbnXVLvDJJYefUVaNzRYrpqN7TAm/view";

    static G2CharacterGuildBook()
    {
        EditorApplication.update += CheckShowWelcomePopup;
    }

    private static void CheckShowWelcomePopup()
    {
        if (!EditorPrefs.HasKey(PlayerPrefsKey) || EditorPrefs.GetInt(PlayerPrefsKey) != 1)
        {
            ShowWelcomePopup();
            EditorApplication.update -= CheckShowWelcomePopup;
        }
    }

    [MenuItem("Window/QuangPhan/G2 Character Guild Book")]
    private static void ShowWelcomePopup()
    {
        G2CharacterGuildBook window = GetWindow<G2CharacterGuildBook>(true, "G2 Character Guild Book");
        window.minSize = new Vector2(600, 800);
        window.maxSize = new Vector2(800, 1200);
        window.ShowUtility();
    }

    private void OnEnable()
    {
        content = new GUIContent(
            "<b>How to use</b>\n\n" +
            "1. How to use Unity Face Capture on G2 Characters\n\n" +
            "2. The characters in the package utilize the Dawnshader_SG shaders created with Shader Graph in Unity.\n" +
            "The package is set up in HDRP mode, if you wish to use them in URP or Built-in, you can switch the SRPs without changing the shaders, " +
            "although you may need to readjust lighting and environment settings.\n" +
            "Additionally, you may need to fine-tune the skin materials or other materials for optimal results.\n" +
            "You may also need to install Shader Graph in Unity to modify the shader and follow the steps below:\n" +
            "Window -> Package Manager -> Shader Graph\n\n" +
            "3. To avoid getting an error log 'inputValue', please follow these steps:\n" +
            "Window -> Package Manager -> At the top left corner of that window, change 'Packages: In Project' to 'Packages: Unity Registry'.\n" +
            "Search for 'Input System' and install it.\n" +
            "Restart the project.\n\n" +
            "4. You should open the Overview scene to find out the features of the character package.\n\n" +
            "5. You can find Face Morphs/Blendshapes on Head Morphs in this package.\n\n" +
            "6. Feel free to learn more about characters and make questions on my Discord." +
            "[My Discord]"
        );
    }

    private void OnGUI()
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(position.height * 0.9f));

        GUIStyle titleStyle = new GUIStyle(EditorStyles.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label("G2 Character Guild Book", titleStyle);

        GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));

        GUIStyle boldStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
        boldStyle.richText = true;

        string[] sections = content.text.Split('\n');

        foreach (string section in sections)
        {
            if (section.StartsWith("1. How to use Unity Face Capture on G2 Characters"))
            {
                GUILayout.Label(section, boldStyle);
                GUILayout.Space(10);
                if (GUILayout.Button("Watch Tutorial Video", GUILayout.Width(200)))
                {
                    Application.OpenURL("https://www.youtube.com/watch?v=aGe-L8yMx_I");
                }
                GUILayout.Space(10);
            }
            else if (section.Contains("[My Discord]"))
            {
                GUILayout.Label(section.Replace("[My Discord]", ""), boldStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Join My Discord", GUILayout.Width(200)))
                {
                    Application.OpenURL(discordInviteLink);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("7. If you have trouble with Green/Pink, it may be because your current HDRP script is missing the character skin profile. Please follow this tutorial to fix it.", boldStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Fix Green/Pink issue tutorial", GUILayout.Width(220)))
                {
                    Application.OpenURL(characterSkinProfileLink);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            else if (section.StartsWith("Window -> Package Manager -> Shader Graph"))
            {
                GUILayout.Label(section, boldStyle);
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Swapping SRPs Tutorial", GUILayout.Width(200)))
                {
                    Application.OpenURL(shaderPackageLink);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            else
            {
                GUILayout.Label(section, boldStyle);
            }
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", GUILayout.Width(100)))
        {
            if (dontShowAgain)
            {
                EditorPrefs.SetInt(PlayerPrefsKey, 1);
            }
            Close();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.FlexibleSpace();
        GUILayout.FlexibleSpace();
        GUILayout.Label("Don't show it again");
        dontShowAgain = GUILayout.Toggle(dontShowAgain, "");
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
    }
}