using EdB.PrepareCarefully;
using Verse;
using WhatsThatMod;

namespace ZooHelper
{
    public class Entry : Mod
    {
        private static AnimalDatabase database;

        public Entry(ModContentPack content) : base(content)
        {
            if(ModCore.DoZooHelper())
                LongEventHandler.QueueLongEvent(Load, "WTM_LoadingMsgZoo", false, null);
        }

        public static void Load()
        {
            database = new AnimalDatabase();
        }

        public static double GetAnimalPoints(Pawn animal)
        {
            if (animal == null || !animal.Spawned || animal.Destroyed || animal.Dead)
                return 0;

            return GetAnimalPoints(animal.def, animal.gender);
        }

        public static double GetAnimalPoints(ThingDef animalDef, Gender gender)
        {
            if (database == null)
            {
                Log.Warning("Called GetAnimalPoints but the database was not loaded, forced to load now.");
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
