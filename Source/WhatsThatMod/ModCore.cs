using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml;
using UnityEngine;
using Verse;

namespace WhatsThatMod
{
    public class ModCore : Mod
    {
        public static ModCore Instance { get; private set; }

        public static bool DoZooHelper()
        {
            return Instance?.GetSettings<WTM_ModSettings>()?.ZooModeEnabled ?? true;
        }

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

            DefWriter(doVanilla, excludedMods, vanillaName, template);
        }

        private static IEnumerable<Def> EnumerateAllDefs()
        {
            bool fast = false;

            if (fast)
            {
                foreach (var mod in LoadedModManager.RunningModsListForReading)
                {
                    if (mod == null)
                        continue;

                    foreach (var def in mod.AllDefs)
                        yield return def;
                }

                foreach (var def in LoadedModManager.PatchedDefsForReading)
                {
                    yield return def;
                }
            }
            else
            {
                foreach (var type in GenDefDatabase.AllDefTypesWithDatabases())
                {
                    foreach(var def in (IEnumerable)GenGeneric.GetStaticPropertyOnGenericType(typeof(DefDatabase<>), type, "AllDefs"))
                    {
                        var finalDef =  def as Def;
                        yield return finalDef;
                    }
                }
            }
        }

        private static void DefWriter(bool doVanilla, HashSet<string> excludedMods, string vanillaName, string template)
        {
            #region Resolve patches
            if (Instance.GetSettings<WTM_ModSettings>().DetectPatched)
            {
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                foreach (var mcp in LoadedModManager.RunningModsListForReading)
                {
                    try
                    {
                        TryResolvePatches(mcp);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.ToString());
                    }
                }

                watch.Stop();
                Log.Message($"  --> [What's That Mod] Took {watch.Elapsed.TotalSeconds:F2} seconds to scan all patches. <--");
            }
            #endregion

            var settings = Instance.GetSettings<WTM_ModSettings>();

            int count = 0;
            int fromPatched = 0;
            foreach (var def in EnumerateAllDefs())
            {
                var mcp = def.modContentPack;
                if (mcp == null)
                {
                    if (probablyAddedBy.TryGetValue(def.defName, out var found))
                    {
                        mcp = found;
                        fromPatched++;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (mcp.IsCoreMod && !doVanilla)
                    continue;

                if(excludedMods.Count > 0 && excludedMods.Contains(mcp.PackageId))
                    continue;

                try
                {
                    string currentDesc = def.description;
                    if (currentDesc == null)
                        continue;

                    bool doCE = settings.CECompat && CE_Compat.IsCEInstalled && CE_Compat.AmmoDefType.IsInstanceOfType(def);

                    string desc = MakeNewDescription(currentDesc, mcp.IsCoreMod ? vanillaName : mcp.Name, template, doCE);

                    if (doCE)
                    {
                        string ce = CE_Compat.GetProjectileReadout(def as ThingDef);
                        desc += $"\n\n<color=#f0d90c><b>{"WTM_AmmoStats".Translate()}</b></color>\n{ce ?? "<null>"}\n\n{CE_Ending}";
                    }

                    def.description = desc;
                    count++;
                }
                catch (Exception e)
                {
                    Log.Error($"What's That Mod: Exception generating def description for [{def.GetType().FullName}] {def.defName} from mod {mcp.Name}:\n{e}");
                }
            }

            probablyAddedBy.Clear();
            Log.Message($"What's That Mod: Wrote to {count} mod descriptions, {fromPatched} patched defs resolved.");
        }

        private static void TryResolvePatches(ModContentPack mod)
        {
            if (mod?.Patches?.EnumerableNullOrEmpty() ?? true)
                return;

            var settings = Instance.GetSettings<WTM_ModSettings>();

            void ExploreAndTag(XmlNode node, ModContentPack mcp, int depth)
            {
                if (node is not XmlElement e)
                    return;

                if (e.Name == "defName")
                {
                    string defName = e.InnerText;
                    if (probablyAddedBy.ContainsKey(defName))
                        return;
                    probablyAddedBy.Add(defName, mcp);
                }
                else
                {
                    if (e.HasChildNodes && (depth == 0 || (settings.UltraDeepMode && depth < 10)))
                    {
                        foreach (XmlNode node2 in e.ChildNodes)
                        {
                            ExploreAndTag(node2, mcp, depth + 1);
                        }
                    }
                }
            }

            foreach (var root in mod.Patches)
            {
                foreach (var patch in ExplorePatchTree(root))
                {
                    foreach (var container in ExtractPatchData(patch))
                    {
                        try
                        {
                            foreach(XmlNode node in container.node.ChildNodes)
                            {
                                ExploreAndTag(node, mod, 0);
                            }
                        }
                        catch { /* Ignore */ }
                    }
                }
            }
        }

        private static FieldInfo GetFieldInfo<T>(string name)
        {
            return typeof(T).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static T GetField<T>(FieldInfo fi, object obj) where T : class
        {
            return fi.GetValue(obj) as T;
        }

        private static readonly Dictionary<string, ModContentPack> probablyAddedBy = new Dictionary<string, ModContentPack>();

        private static readonly FieldInfo cond_match = GetFieldInfo<PatchOperationConditional>("match");
        private static readonly FieldInfo cond_nomatch = GetFieldInfo<PatchOperationConditional>("nomatch");
        private static readonly FieldInfo find_match = GetFieldInfo<PatchOperationFindMod>("match");
        private static readonly FieldInfo find_nomatch = GetFieldInfo<PatchOperationFindMod>("nomatch");
        private static readonly FieldInfo seq_ops = GetFieldInfo<PatchOperationSequence>("operations");

        private static readonly FieldInfo add_value = GetFieldInfo<PatchOperationAdd>("value");
        private static readonly FieldInfo insert_value = GetFieldInfo<PatchOperationInsert>("value");

        private static IEnumerable<PatchOperation> ExplorePatchTree(PatchOperation patch)
        {
            if (patch == null)
                yield break;

            switch (patch)
            {
                case PatchOperationFindMod find:
                    foreach (var thing in ExplorePatchTree(GetField<PatchOperation>(find_match, find)))
                        yield return thing;
                    foreach (var thing in ExplorePatchTree(GetField<PatchOperation>(find_nomatch, find)))
                        yield return thing;
                    break;

                case PatchOperationConditional cond:
                    foreach (var thing in ExplorePatchTree(GetField<PatchOperation>(cond_match, cond)))
                        yield return thing;
                    foreach (var thing in ExplorePatchTree(GetField<PatchOperation>(cond_nomatch, cond)))
                        yield return thing;
                    break;

                case PatchOperationSequence seq:
                    var list = GetField<List<PatchOperation>>(seq_ops, seq);
                    if (list == null)
                        break;
                    
                    foreach (var item in list)
                    {
                        if (item == null)
                            continue;

                        yield return item;
                        foreach (var found in ExplorePatchTree(item))
                            yield return found;
                    }
                    
                    break;

                default:
                    yield return patch;
                    break;
            }
        }

        private static IEnumerable<XmlContainer> ExtractPatchData(PatchOperation op)
        {
            if (op == null)
                yield break;

            switch (op)
            {
                case PatchOperationAdd add:
                    yield return GetField<XmlContainer>(add_value, add);
                    break;

                case PatchOperationInsert insert:
                    yield return GetField<XmlContainer>(insert_value, insert);
                    break;
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

        private static string CE_Ending;
        public static string MakeNewDescription(string currentDesc, string rawModName, string template, bool checkCE)
        {
            // Sanitize mod name.
            string modName = rawModName.Replace('[', '(').Replace(']', ')');

            string current = currentDesc.TrimEnd();
            if (checkCE)
            {
                CE_Ending ??= "CE_UsedBy".Translate() + ":";
                if (current.EndsWith(CE_Ending))
                {
                    current = current.Substring(0, current.Length - CE_Ending.Length).TrimEnd();
                }
            }

            return current + string.Format(template, modName);
        }

        public ModCore(ModContentPack mcp) : base(mcp)
        {
            Instance = this;

            Log.Message("Loaded What's That Mod.");
            LongEventHandler.QueueLongEvent(Run, "WTM_LoadingMsg", false, null);
        }

        private void Run()
        {
            var sw = new Stopwatch();
            sw.Start();
            WriteToDefs();
            sw.Stop();
            Log.Message($"What's That Mod took {sw.ElapsedMilliseconds} milliseconds to generate all def descriptions.");
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
