using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace WhatsThatMod
{
    public class ModCore : Mod
    {
        public static ModCore Instance { get; private set; }

        public static void WriteToDefs()
        {
            // Find mod settings.
            if (Instance == null)
            {
                Log.Error("Failed to find ModCore instance... Why?!");
                return;
            }

            var settings = Instance.GetSettings<WTM_ModSettings>();
            if (settings == null)
            {
                Log.Error("Failed to get settings from ModCore. Why?!");
                return;
            }

            string template = MakeTemplate(settings);
            bool doVanilla = settings.FlagVanilla;
            string vanillaName = settings.VanillaName;
            HashSet<string> excludedMods = new HashSet<string>();
            if (settings.ExcludedMods != null)
            {
                foreach (var modName in settings.ExcludedMods)
                {
                    if (string.IsNullOrWhiteSpace(modName))
                        continue;

                    bool worked = excludedMods.Add(modName.Trim());
                    if (!worked)
                        Log.Warning($"Duplicate excluded mod entry: {modName.Trim()}");

                    // Try to find the actual mod based on this ID.
                    // If it isn't found, the user probably typed the ID wrong.
                    var meta = ModLister.GetActiveModWithIdentifier(modName.Trim());
                    if(meta == null)
                        Log.Warning($"Failed to find active mod with ID '{modName.Trim()}'. Mod won't be excluded.");
                }
            }
            HashSet<Type> excludedDefTypes = new HashSet<Type>();
            if (settings.ExcludedDefTypes != null)
            {
                Type defType = typeof(Def);
                foreach (var rawTypeName in settings.ExcludedDefTypes)
                {
                    // Try to get type from name. This may or may not work!
                    string typeName = rawTypeName?.Trim();
                    if (string.IsNullOrEmpty(typeName))
                        continue;

                    Type t = Type.GetType(typeName, false, true);
                    if (t == null)
                    {
                        Log.Error($"Failed to find excluded def Type for name {typeName}. Perhaps it's from a mod that isn't active?");
                        continue;
                    }
                    if (!defType.IsAssignableFrom(t))
                    {
                        Log.Error($"Type '{t.FullName}' does not inherit from {defType.FullName}. Will be ignored.");
                        continue;
                    }

                    bool wasAdded = excludedDefTypes.Add(t);
                    if (!wasAdded)
                        Log.Warning($"Duplicate excluded def type entry: {t.FullName}");
                }
            }

            foreach (var mcp in LoadedModManager.RunningMods)
            {
                if (mcp == null)
                    continue;

                try
                {
                    if (mcp.IsCoreMod && !doVanilla)
                        continue;

                    var meta = ModLister.GetActiveModWithIdentifier(mcp.PackageId);
                    if (meta == null)
                    {
                        Log.Warning($"Failed to get meta from active mod '{mcp.Name}' ({mcp.PackageId}). This is normally caused by having a local copy and also the steam version installed. Mod can't be checked for exclusion.");
                    }
                    else
                    {
                        //Log.Message($"Got meta for: {mcp.Name} ({mcp.PackageId}) [{mcp.PackageIdPlayerFacing}]");
                        bool exclude = false;
                        foreach (var excluded in excludedMods)
                        {
                            if (meta.SamePackageId(excluded, true))
                            {
                                exclude = true;
                                break;
                            }
                        }

                        if (exclude)
                        {
                            Log.Message($"Excluding mod '{meta.Name}' from What's That Mod");
                            continue;
                        }
                    }

                    var defs = mcp.AllDefs;
                    if (defs == null)
                        continue;

                    foreach (var def in defs)
                    {
                        if (def == null)
                            continue;

                        Type defType = def.GetType();

                        try
                        {
                            string currentDesc = def.description;
                            if (currentDesc == null)
                                continue;

                            string desc = MakeNewDescription(currentDesc, mcp.Name, template);

                            def.description = desc;
                        }
                        catch (Exception e)
                        {
                            Log.Error($"What's That Mod: Exception generating def description for [{defType.Name}] {def.defName} from mod {mcp.Name}:\n{e}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"What's That Mod: Exception when parsing mod's Defs. Mod is {mcp.Name}. Exception:\n{e}");
                }
            }
        }

        private static readonly StringBuilder str = new StringBuilder();
        public static string MakeTemplate(WTM_ModSettings settings)
        {
            str.Clear();
            for (int i = 0; i <= settings.BlankLines; i++)
            {
                str.Append('\n');
            }

            if (settings.Italics)
                str.Append("<i>");
            if (settings.Bold)
                str.Append("<b>");
            if (settings.CustomSize > 0)
                str.Append("<size=").Append(settings.CustomSize).Append('>');
            str.Append("<color=#").Append(settings.ColorHex).Append('>');

            bool formatIsValid = settings.Format != null && settings.Format.Contains("{0}");
            try
            {
                if (formatIsValid)
                    _ = string.Format(settings.Format.Replace('[', '(').Replace(']', ')'), "ExampleModName");
            }
            catch (Exception e)
            {
                formatIsValid = false;
                _ = e;
            }
            string format = formatIsValid ? settings.Format.Replace('[', '(').Replace(']', ')') : "{0}";
            str.Append(format);

            str.Append("</color>");
            if (settings.CustomSize > 0)
                str.Append("</size>");
            if (settings.Bold)
                str.Append("</b>");
            if (settings.Italics)
                str.Append("</i>");

            return str.ToString();
        }

        public static string MakeNewDescription(string currentDesc, string rawModName, string template)
        {
            // Sanitize mod name.
            string modName = rawModName.Replace('[', '(').Replace(']', ')');

            return currentDesc.TrimEnd() + string.Format(template, modName);
        }

        public ModCore(ModContentPack mcp) : base(mcp)
        {
            Instance = this;
            var settings = GetSettings<WTM_ModSettings>(); // Needs to be called to initialize settings.
            if (settings.IsBroken)
            {
                // This indicates a bug. Mod settings file needs to be deleted, and settings reset.
                var newSettings = new WTM_ModSettings();

                var modField = typeof(ModSettings).GetProperty("Mod", BindingFlags.Public | BindingFlags.Instance);
                var settingsField = typeof(Mod).GetField("modSettings", BindingFlags.NonPublic | BindingFlags.Instance);

                Log.Message("<color=magenta>WTM: Detected broken mod settings, trying to fix...");
                //Log.Message($"mf: {modField}, sf: {settingsField}");
                modField.SetValue(newSettings, this);
                settingsField.SetValue(this, newSettings);
                bool worked = newSettings == GetSettings<WTM_ModSettings>() && newSettings.Mod == this;
                Log.Message($"<color=magenta>WTM: Detected broken settings, attempted fix. Worked: {worked}</color>");
            }

            Log.Message("Loaded What's That Mod. Def descriptions will be written to in static constructor.");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            GetSettings<WTM_ModSettings>().DrawWindow(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "What's That Mod";
        }
    }
}
