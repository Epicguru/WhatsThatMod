using RimWorld;
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Verse;

namespace WhatsThatMod
{
    public static class CE_Compat
    {
        public static bool IsCEInstalled { get; }
        public static Type AmmoDefType { get; }

        private static StatDef ammoStat;
        private static FieldInfo defInfo;
        private static MethodInfo method;

        static CE_Compat()
        {
            IsCEInstalled = ModLister.GetActiveModWithIdentifier("CETeam.CombatExtended") != null;
            if (IsCEInstalled)
            {
                AmmoDefType = GenTypes.GetTypeInAnyAssembly("CombatExtended.AmmoDef");
                method = GenTypes.GetTypeInAnyAssembly("CombatExtended.AmmoUtility")?.GetMethod("GetProjectileReadout", BindingFlags.Public | BindingFlags.Static);
            }
        }

        public static string GetProjectileReadout(ThingDef ammoThingDef)
        {
            //IEnumerable list = ammoThingDef.GetType().GetProperty("AmmoSetDefs").GetValue(ammoThingDef) as IEnumerable;
            //if (list == null)
            //    return "NULL AMMOSETDEFS";
            //StringBuilder str = new StringBuilder();
            //Log.Message(list.ToString() + " for " + ammoThingDef.LabelCap);
            //foreach (var ammoSetDef in list)
            //{
            //    var links = ammoSetDef.GetType().GetField("ammoTypes", BindingFlags.Public | BindingFlags.Instance).GetValue(ammoSetDef) as IEnumerable;
            //    if (links != null)
            //    {
            //        Log.Message("HERE!!");
            //        foreach (var link in links)
            //        {
            //            var projectile = link.GetType().GetField("projectile").GetValue(link) as ThingDef;
            //            Log.Message(projectile.ToString());
            //            string readout = method.Invoke(null, new object[] { projectile, null }) as string;
            //            str.Append(readout);
            //        }
            //    }
            //    else
            //    {
            //        return "NULL LINKS";
            //    }
            //}
            //return str.ToString();


            ammoStat ??= StatDef.Named("AmmoCaliber");
            if (ammoStat == null)
            {
                return "Error generating stats. Please report this bug.";
            }

            // Incoming: reflection hell.

            defInfo ??= typeof(StatRequest).GetField("defInt", BindingFlags.NonPublic | BindingFlags.Instance);

            StatRequest req = new StatRequest();
            object obj = RuntimeHelpers.GetObjectValue(req);
            defInfo.SetValue(obj, ammoThingDef);
            req = (StatRequest)obj;

            string info = ammoStat.Worker.GetExplanationUnfinalized(req, ToStringNumberSense.Factor);
            return info;
        }
    }
}
