using System.Diagnostics;
using Verse;

namespace WhatsThatMod
{
    [StaticConstructorOnStartup]
    public static class StaticLoader
    {
        static StaticLoader()
        {
            var sw = new Stopwatch();
            sw.Start();
            ModCore.WriteToDefs();
            sw.Stop();
            Log.Message($"What's That Mod took {sw.ElapsedMilliseconds} milliseconds to generate all def descriptions.");
        }
    }
}
