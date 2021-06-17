using EdB.PrepareCarefully;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using WhatsThatMod;

namespace ZooHelper
{
    public class Main : Mod
    {
        public static bool IsLoaded => database != null;
        public static readonly List<Pawn> CurrentAnimals = new List<Pawn>();
        public static readonly List<Pawn> AllTameAnimals = new List<Pawn>();
        public static double CurrentPoints { get; private set; }

        private static AnimalDatabase database;
        private static MethodInfo trainingMethod;
        private static object[] args = new object[1];
        private static Dictionary<ThingDef, (Pawn animal, double points)> animalsByDef = new Dictionary<ThingDef, (Pawn, double)>();

        public Main(ModContentPack content) : base(content)
        {
            Log.Message($"<color=magenta>Loaded ZooHelper</color>");
        }

        public static void Load()
        {
            database = new AnimalDatabase();
        }

        public static void Recalculate()
        {
            if (!ModCore.DoZooHelper())
                return;

            CurrentAnimals.Clear();
            AllTameAnimals.Clear();
            animalsByDef.Clear();

            foreach (var map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    var things = map.listerThings.AllThings;
                    if (things == null)
                        return;

                    foreach (var thing in things)
                    {
                        if (thing == null || thing.Destroyed)
                            continue;

                        Pawn pawn = null;
                        if (thing is Building_Casket casket)
                        {
                            if (casket.ContainedThing is Pawn p)
                                pawn = p;
                        }
                        else if (thing is Pawn p)
                        {
                            pawn = p;
                        }

                        if (pawn == null)
                            continue;

                        if (pawn.RaceProps.Animal)
                        {
                            double points = GetAnimalPoints(pawn);
                            if (points > 0)
                            {
                                AllTameAnimals.Add(pawn);
                                if (animalsByDef.TryGetValue(pawn.def, out var pair))
                                {
                                    if (pair.points < points)
                                        CurrentAnimals.Remove(pair.animal);
                                    else
                                        continue;
                                }

                                animalsByDef[pawn.def] = (pawn, points);
                                CurrentAnimals.Add(pawn);
                            }
                        }
                    }
                }
            }

            double p2 = 0;
            foreach (var animal in CurrentAnimals)
            {
                p2 += GetAnimalPoints(animal);
            }
            CurrentPoints = p2;
            CurrentAnimals.SortByDescending(GetAnimalPoints);
        }

        public static double GetAnimalPoints(Pawn animal)
        {
            if (animal == null || animal.Dead)
                return 0;

            if (animal.training == null)
                return 0;

            if (!animal.Faction.IsPlayer)
                return 0;

            bool isWild = true;
            trainingMethod ??= typeof(Pawn_TrainingTracker).GetMethod("GetSteps", BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                args[0] = TrainableDefOf.Tameness;
                isWild = (int) trainingMethod.Invoke(animal.training, args) <= 0;
            }
            catch
            {
                // ignored
            }

            if (isWild)
                return 0;

            return GetAnimalPoints(animal.def, animal.gender);
        }

        public static double GetAnimalPoints(ThingDef animalDef, Gender gender)
        {
            if (database == null)
            {
                Log.Error("Called GetAnimalPoints but the database was not loaded, forced to load now.");
                Load();
            }

            if (animalDef == null)
                return 0;

            var record = database.FindAnimal(new AnimalRecordKey(animalDef, gender));
            if (record == null)
                return 0;

            return record.Cost * 0.01;
        } 
    }
}
