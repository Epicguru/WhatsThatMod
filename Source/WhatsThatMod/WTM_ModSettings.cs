using System;
using System.Collections.Generic;
using ColourPicker;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace WhatsThatMod
{
    public class WTM_ModSettings : ModSettings
    {
        public bool IsBroken
        {
            get
            {
                return VanillaName == null;
            }
        }

        public bool FlagVanilla = false;
        public string VanillaName = "Rimworld";

        public string Format = "{0}";
        public bool Italics = true;
        public bool Bold = false;
        public int CustomSize = 0;
        public string ColorHex = "66E0E4FF";
        public int BlankLines = 1;
        public bool DetectPatched = true;
        public bool UltraDeepMode = false;
        public bool CECompat = true;

        public List<string> ExcludedMods = new List<string>();
        public List<string> ExcludedDefTypes = new List<string>();

        private bool showAdvanced;
        private Texture2D currentTexture;
        private string currentTextureColor = "INVALID";
        private Window colorPicker;
        private string fontSizeBuffer = "";
        private string blankLinesBuffer = "";
        private Vector2 modsScroll;

        public Color GetFontColor()
        {
            bool worked = ColorUtility.TryParseHtmlString('#' + ColorHex, out var color);

            if(!worked)
                Log.Error($"Failed to parse '#{ColorHex}' as color.");

            return worked ? color : Color.white;
        }

        public Texture2D GetCurrentColorTexture()
        {
            if (currentTexture == null || currentTextureColor != ColorHex)
            {
                currentTextureColor = ColorHex;
                if (currentTexture != null)
                {
                    Object.Destroy(currentTexture);
                }
                currentTexture = SolidColorMaterials.NewSolidColorTexture(GetFontColor());
            }

            return currentTexture;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref FlagVanilla, "FlagVanilla", false);
            Scribe_Values.Look(ref VanillaName, "VanillaName", "Rimworld");

            Scribe_Values.Look(ref Format, "Format", "{0}");
            Scribe_Values.Look(ref Bold, "Bold", true);
            Scribe_Values.Look(ref Italics, "Italics", true);
            Scribe_Values.Look(ref CustomSize, "CustomSize", 0);
            Scribe_Values.Look(ref ColorHex, "ColorHex", "66E0E4FF");
            Scribe_Values.Look(ref BlankLines, "BlankLines", 1);
            Scribe_Values.Look(ref DetectPatched, "DetectPatched", true);
            Scribe_Values.Look(ref UltraDeepMode, "UltraDeepMode", false);
            Scribe_Values.Look(ref CECompat, "CECompat", true);

            Scribe_Collections.Look(ref ExcludedMods, "ExcludedMods", LookMode.Value);
            if (ExcludedMods == null)
                ExcludedMods = new List<string>();
            Scribe_Collections.Look(ref ExcludedDefTypes, "ExcludedDefTypes", LookMode.Value);
            if (ExcludedDefTypes == null)
                ExcludedDefTypes = new List<string>();

            base.ExposeData();
        }

        public void DrawWindow(Rect rect)
        {
            // Draw all settings.
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x, rect.y, Mathf.Min(rect.width, 320), rect.height));

            listing.Label("<color=yellow>" + "WTM_RequireRestart".Translate() + "</color>");
            listing.GapLine();

            listing.CheckboxLabeled("WTM_FlagVanilla".Translate(), ref FlagVanilla, "WTM_FlagVanilla_Desc".Translate(VanillaName.Trim()));
            VanillaName = listing.TextEntryLabeled("WTM_VanillaName".Translate(), VanillaName);
            listing.Gap();

            listing.Label("<b>" + "WTM_VisualOptions".Translate() + "</b>");

            listing.Label("WTM_Format".Translate(), tooltip: "WTM_Format_Desc".Translate());
            Format = listing.TextEntry(Format);
            if (!Format.Contains("{0}"))
                listing.Label("<color=red>" + "WTM_Format_MissingTag".Translate() + "</color>");
            try
            {
                _ = string.Format(Format.Replace('[', '(').Replace(']', ')'), "ExampleModName");
            }
            catch (Exception e)
            {
                _ = e;
                listing.Label("<color=red>" + "WTM_Format_FormatError".Translate() + "</color>");
            }

            listing.CheckboxLabeled("WTM_Italics".Translate(), ref Italics, "WTM_Italics_Desc".Translate());
            listing.CheckboxLabeled("WTM_Bold".Translate(), ref Bold, "WTM_Bold_Desc".Translate());

            listing.Label("WTM_CustomSize".Translate(CustomSize), tooltip: "WTM_CustomSize_Desc".Translate());
            listing.IntEntry(ref CustomSize, ref fontSizeBuffer);
            CustomSize = Mathf.Clamp(CustomSize, 0, 64);
            fontSizeBuffer = CustomSize.ToString();

            listing.Label("WTM_BlankLines".Translate(BlankLines), tooltip: "WTM_BlankLines_Desc".Translate());
            listing.IntEntry(ref BlankLines, ref blankLinesBuffer);
            BlankLines = Mathf.Clamp(BlankLines, 0, 12);
            blankLinesBuffer = BlankLines.ToString();

            listing.Label("WTM_SelectFontColor".Translate(), tooltip: "WTM_SelectFontColor_Desc".Translate());
            bool openColorPicker = listing.ButtonImage(GetCurrentColorTexture(), 100, 32);
            if (openColorPicker)
                OpenColorPicker();

            // Do preview box.
            float y = listing.CurHeight + rect.y - 30;
            Rect previewBox = new Rect(rect.x, y, 310, 170);
            Widgets.DrawBox(previewBox);
            previewBox = previewBox.ExpandedBy(-5);
            string rawText = "WTM_ExampleDescription".Translate();
            string template = ModCore.MakeTemplate(this);
            string text = ModCore.MakeNewDescription(rawText, "Example Mod", template, false);
            Widgets.Label(previewBox, text);

            listing.End();

            listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x + 350, rect.y, 320, rect.height));
            if(listing.ButtonText($"{(showAdvanced ? "WTM_Hide".Translate() : "WTM_Show".Translate())} {"WTM_AdvancedSettings".Translate()}"))
                showAdvanced = !showAdvanced;

            if (showAdvanced)
            {
                // Draw advanced settings.
                listing.GapLine();

                listing.CheckboxLabeled("WTM_CECompat".Translate(), ref CECompat, "WTM_CECompat_Desc".Translate());
                listing.CheckboxLabeled("WTM_DetectPatched".Translate(), ref DetectPatched, "WTM_DetectPatched_Desc".Translate());

                if (!DetectPatched)
                    GUI.enabled = false;
                listing.CheckboxLabeled("WTM_UltraDeepMode".Translate(), ref UltraDeepMode, "WTM_UltraDeepMode_Desc".Translate());
                GUI.enabled = true;

                listing.Label("WTM_ExcludedMods".Translate(), tooltip: "WTM_ExcludedMods_Desc".Translate());
                bool addNew = listing.ButtonText("WTM_AddNew".Translate());
                bool removeBlank = listing.ButtonText("WTM_RemoveBlank".Translate());
                listing.Gap();

                if (addNew)
                    ExcludedMods.Add("");
                if (removeBlank)
                {
                    for (int i = 0; i < ExcludedMods.Count; i++)
                    {
                        string txt = ExcludedMods[i];
                        if (string.IsNullOrWhiteSpace(txt))
                        {
                            ExcludedMods.RemoveAt(i);
                            i--;
                        }
                    }
                }
                for(int i = 0; i < ExcludedMods.Count; i++)
                {
                    var mod = ExcludedMods[i];
                    mod = listing.TextEntry(mod);
                    ExcludedMods[i] = mod;

                    string final = mod.Trim();
                    var meta = ModLister.GetModWithIdentifier(final, true);
                    string msg = meta == null ? "WTM_ModNotFound".Translate() : "WTM_ModFound".Translate(meta.Name);
                    string color = meta == null ? "red" : "green";
                    listing.Label($"<color={color}>{msg}</color>");
                    listing.GapLine();
                }
            }

            listing.End();
        }

        private void OpenColorPicker()
        {
            if (colorPicker != null && colorPicker.IsOpen)
                return;

            var currentColor = GetFontColor();
            var dialog = new Dialog_ColourPicker(currentColor, (newColor) =>
            {
                ColorHex = ColorUtility.ToHtmlStringRGBA(newColor);
            });
            dialog.minimalistic = true;
            colorPicker = dialog;
            Find.WindowStack.Add(dialog);
        }
    }
}
