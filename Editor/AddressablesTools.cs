using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.AddressableAssets.GUI;
using static UnityEditor.AddressableAssets.GUI.AddressableAssetsSettingsGroupEditor;

static class AddressablesTools
{
    static readonly System.Type ToolbarType = typeof(EditorGUI).Assembly.GetType("UnityEditor.Toolbar");
    static readonly FieldInfo get = ToolbarType.GetField(nameof(get), BindingFlags.Static | BindingFlags.Public);
    static readonly FieldInfo m_Root = ToolbarType.GetField(nameof(m_Root), BindingFlags.Instance | BindingFlags.NonPublic);

    static EditorToolbarDropdown m_ToolbarButton;

    [InitializeOnLoadMethod]
    static void OnLoad()
    {
        EditorApplication.update += OnUpdate;
    }

    static void OnUpdate()
    {
        var toolbar = get.GetValue(null);

        if (toolbar != null)
        {
            var root = m_Root.GetValue(toolbar) as VisualElement;
            var rightToolbar = root.Query<VisualElement>("ToolbarZoneRightAlign").First();

            if (rightToolbar != null)
            {
                rightToolbar.Add(m_ToolbarButton = new EditorToolbarDropdown("Addressables", OnDropdownClick));
                EditorApplication.update -= OnUpdate;
            }
        }
    }

    static void OnDropdownClick()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            return;
        }

        var menu = new GenericMenu();

        void AddAddressablesMenuItem(string menuPath)
        {
            menu.AddItem(new GUIContent(System.IO.Path.GetFileName(menuPath)), false, () =>
            {
                EditorApplication.ExecuteMenuItem(menuPath);
            });
        }

        void AddMenuItem(string menuPath, GenericMenu.MenuFunction action)
        {
            menu.AddItem(new GUIContent(menuPath), false, action);
        }

        AddAddressablesMenuItem("Window/Asset Management/Addressables/Groups");
        AddAddressablesMenuItem("Window/Asset Management/Addressables/Settings");

        AddMenuItem("Profiles/Edit", () =>
        {
            EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Profiles");
        });

        var profileNames = settings.profileSettings.GetAllProfileNames();

        if (0 < profileNames.Count)
        {
            menu.AddSeparator("Profiles/");
            foreach (var profileName in profileNames)
            {
                var profileId = settings.profileSettings.GetProfileId(profileName);
                menu.AddItem(new GUIContent("Profiles/" + profileName), profileId == settings.activeProfileId, () =>
                {
                    settings.activeProfileId = profileId;
                });
            }
        }

        AddAddressablesMenuItem("Window/Asset Management/Addressables/Event Viewer");
        AddAddressablesMenuItem("Window/Asset Management/Addressables/Analyze");

        AddMenuItem("Hosting/Edit", () =>
        {
            EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Hosting");
        });

        if (0 < settings.HostingServicesManager.HostingServices.Count)
        {
            menu.AddSeparator("Hosting/");
            foreach (var item in settings.HostingServicesManager.HostingServices)
            {
                var service = item;
                menu.AddItem(new GUIContent("Hosting/" + service.DescriptiveName), service.IsHostingServiceRunning, () =>
                {
                    if (service.IsHostingServiceRunning)
                    {
                        service.StopHostingService();
                    }
                    else
                    {
                        service.StartHostingService();
                    }
                });
            }
        }

        menu.AddSeparator("");

        for (int i = 0; i < settings.DataBuilders.Count; i++)
        {
            var m = settings.GetDataBuilder(i);
            if (m.CanBuildData<AddressablesPlayModeBuildResult>())
            {
                var index = i;
                menu.AddItem(new GUIContent("Play Mode/" + m.Name), i == settings.ActivePlayModeDataBuilderIndex, () =>
                {
                    AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = index;
                });
            }
        }

        var types = AddressableAssetUtility.GetTypes<IAddressablesBuildMenu>();
        var displayMenus = CreateBuildMenus(types);
        foreach (IAddressablesBuildMenu buildOption in displayMenus)
        {
            if (buildOption.SelectableBuildScript)
            {
                bool addressablesPlayerBuildResultBuilderExists = false;
                for (int i = 0; i < settings.DataBuilders.Count; i++)
                {
                    var dataBuilder = settings.GetDataBuilder(i);
                    if (dataBuilder.CanBuildData<AddressablesPlayerBuildResult>())
                    {
                        addressablesPlayerBuildResultBuilderExists = true;
                        BuildMenuContext context = new BuildMenuContext()
                        {
                            buildScriptIndex = i,
                            BuildMenu = buildOption,
                            Settings = settings
                        };

                        menu.AddItem(new GUIContent("Build/" + buildOption.BuildMenuPath + "/" + dataBuilder.Name), false, OnBuildAddressables, context);
                    }
                }

                if (!addressablesPlayerBuildResultBuilderExists)
                    menu.AddDisabledItem(new GUIContent("Build/" + buildOption.BuildMenuPath + "/No Build Script Available"));
            }
            else
            {
                BuildMenuContext context = new BuildMenuContext()
                { buildScriptIndex = -1, BuildMenu = buildOption, Settings = settings };
                menu.AddItem(new GUIContent("Build/" + buildOption.BuildMenuPath), false, OnBuildAddressables, context);
            }
        }

        menu.AddSeparator("Build/Clear Build Cache/");

        menu.AddItem(new GUIContent("Build/Clear Build Cache/All"), false, ()=>
        {
            AddressableAssetSettings.CleanPlayerContent(null);
            BuildCache.PurgeCache(true);
        });
        menu.AddItem(new GUIContent("Build/Clear Build Cache/Content Builders/All"), false, () =>
        {
            AddressableAssetSettings.CleanPlayerContent(null);
        });

        for (int i = 0; i < settings.DataBuilders.Count; i++)
        {
            var m = settings.GetDataBuilder(i);
            menu.AddItem(new GUIContent("Build/Clear Build Cache/Content Builders/" + m.Name), false, () =>
            {
                AddressableAssetSettings.CleanPlayerContent(m);
            });
        }

        menu.AddItem(new GUIContent("Build/Clear Build Cache/Build Pipeline Cache"), false, ()=>
        {
            BuildCache.PurgeCache(true);
        });

        menu.DropDown(m_ToolbarButton.worldBound);
    }

    static void OnBuildAddressables(object ctx)
    {
        AddressableAssetsSettingsGroupEditor.OnBuildAddressables((BuildMenuContext)ctx);
    }
}
