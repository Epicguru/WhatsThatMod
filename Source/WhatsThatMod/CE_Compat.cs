using RimWorld;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            ammoStat ??= StatDef.Named("AmmoCaliber");
            if (ammoStat == null)
                return "Error generating stats. Please report this bug.";

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
