using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using WhatsThatMod;

namespace ZooHelper
{
    public class UI : GameComponent
    {
        public UI(Game game) { }

        private int tickCounter;

        public override void GameComponentTick()
        {
            tickCounter++;

            float multi = Find.TickManager.TickRateMultiplier;
            if (multi < 1)
                multi = 1;

            if (tickCounter % Mathf.RoundToInt(60 * 10 * multi) == 0)
            {
                if (ModCore.DoMultithread())
                {
                    if (!Main.IsLoaded)
                        Main.Load();

                    Task.Run(() =>
                    {
                        try
                        {
                            Main.Recalculate();
                        }
                        catch (Exception e)
                        {
                            Log.Warning("Exception during zoo score recalculation. If you see this error a lot, disable multithreading in mod settings.");
                            Log.Warning(e.ToString());
                        }
                    });
                }
                else
                {
                    Main.Recalculate();
                }
            }
        }

        public override void GameComponentOnGUI()
        {
            if (Find.UIRoot.screenshotMode.FiltersCurrentEvent || !ModCore.DoZooHelper())
                return;

            var area = new Rect(Verse.UI.screenWidth - 260, 100, 250, 150);
            float a = 0.5f;
            if (area.ExpandedBy(10).Contains(Verse.UI.MousePositionOnUIInverted))
            {
                a = 1f;
                if (Input.GetMouseButtonDown(0))
                {
                    Find.WindowStack.Add(new MyWindow());
                }
            }

            GUI.color = new Color(1, 1, 1, a);
            GUILayout.BeginArea(area);
            Draw(() => GUILayout.Label($"<b><size=25>Zoo Score: <color=#6bff26{(a == 1f ? "ff" : "88")}>{Main.CurrentPoints:F1}</color></size></b>"));
            Draw(() => GUILayout.Label($"Total zoo animals: {Main.AllTameAnimals.Count} ({Main.CurrentAnimals.Count} give points)"));
            Draw(() => GUILayout.Label("<i>Click for more info.</i>"));
            GUILayout.EndArea();
            GUI.color = Color.white;
        }

        private void Draw(Action a)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            a();
            GUILayout.EndHorizontal();
        }
    }

    public class MyWindow : Window
    {
        Listing_Standard listing = new Listing_Standard();

        public MyWindow()
        {
            doCloseButton = true;
            doCloseX = false;
            forcePause = false;
            draggable = true;
            resizeable = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            listing.Begin(new Rect(rect.x, rect.y, rect.width, rect.height));
            listing.Label("<i><color=cyan>Added by What's That Mod. Can be turned on or off in mod settings.</color></i>");
            listing.Gap();
            listing.Label($"Total zoo score: {Main.CurrentPoints:F1}");
            listing.Label($"Total animals: {Main.AllTameAnimals.Count}");
            listing.Label("<i>Note: Does not include bonus achievement points, so calculate those yourself!</i>");
            listing.GapLine();
            listing.Label($"Point-giving animals ({Main.CurrentAnimals.Count}):");
            StringBuilder str = new StringBuilder();
            foreach (var animal in Main.CurrentAnimals)
            {
                if (animal == null || animal.Dead)
                    continue;

                string root = animal.def.LabelCap + (animal.gender == Gender.Male ? " [M]" : " [F]") + ":  ";
                string end = "  " + Main.GetAnimalPoints(animal).ToString("F2");
                int len = root.Length + end.Length;
                int remaining = 90 - len;
                string mid = "";
                if (remaining > 0)
                    mid = new string('.', remaining);
                str.AppendLine(root + mid + end);
                
            }

            listing.Label(str.ToString(), rect.height - listing.CurHeight - 45);
            listing.End();
        }
    }
}
