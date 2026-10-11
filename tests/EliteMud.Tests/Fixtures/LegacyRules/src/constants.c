/* Cut-down constants.c for LegacyRulesImporterTests */

const char	*class_abbrevs[] = {
   "-- --",
   "#MMu#N   ",
   "#RWa#N   ",
   "#wUn#N   ",
   "#wDu#N   ",
   "\n"
};

const char     *race_table[]  =  {
    "god",
    "human",
    "dwarf",
    "angel",
    "\n"
};

const char   *race_title_names[]  =  {
    "#rDivine#N",
    "#wHuman#N",
    "#RDwarven#N",
    "#WAngelic#N",
    "\n"
};

/*                 god hum dwa ang */
const int str_max[]= {  25, 18, 20, 21 };
const int con_max[]= {  25, 18, 20, 19 };
const int dex_max[]= {  25, 18, 16, 20 };
const int int_max[]= {  25, 18, 16, 22 };
const int wis_max[]= {  25, 18, 18, 23 };
const int cha_max[]= {  25, 18, 18, 24 };

const int hp_start[] = { 100, 50, 55, 60 };
const int mn_start[] = { 100, 100, 80, 120 };
const int mv_start[] = { 100, 80, 75, 90 };

const int age_average[] = { 1000, 20, 80, 500 };
const int height_average[] = { 72, 70, 48, 74 };
const int weight_average[] = { 200, 170, 135, 150 };

/* Angel has no entry, as in legacy: the import pads it with 0 and warns */
const long  allowed_classes[] = {
    0 ,     /* god - noneclass */
    MU_CLASS | WA_CLASS | UN_CLASS,   /* human */
    WA_CLASS    /* dwarf */
};

const struct title_type titles[3][40] = {
    /* Magic-User */
{
  { "Mage 0", "Mage 0ess", 1 },
  { "Mage 1", "Mage 1ess", 1000 },
  { "Mage 2", "Mage 2ess", 2000 },
  { "Mage 3", "Mage 3ess", 3000 },
  { "Mage 4", "Mage 4ess", 4000 },
  { "Mage 5", "Mage 5ess", 5000 },
  { "Mage 6", "Mage 6ess", 6000 },
  { "Mage 7", "Mage 7ess", 7000 },
  { "Mage 8", "Mage 8ess", 8000 },
  { "Mage 9", "Mage 9ess", 9000 },
  { "Mage 10", "Mage 10ess", 10000 },
  { "Mage 11", "Mage 11ess", 11000 },
  { "Mage 12", "Mage 12ess", 12000 },
  { "Mage 13", "Mage 13ess", 13000 },
  { "Mage 14", "Mage 14ess", 14000 },
  { "Mage 15", "Mage 15ess", 15000 },
  { "Mage 16", "Mage 16ess", 16000 },
  { "Mage 17", "Mage 17ess", 17000 },
  { "Mage 18", "Mage 18ess", 18000 },
  { "Mage 19", "Mage 19ess", 19000 },
  { "Mage 20", "Mage 20ess", 20000 },
  { "Mage 21", "Mage 21ess", 21000 },
  { "Mage 22", "Mage 22ess", 22000 },
  { "Mage 23", "Mage 23ess", 23000 },
  { "Mage 24", "Mage 24ess", 24000 },
  { "Mage 25", "Mage 25ess", 25000 },
  { "Mage 26", "Mage 26ess", 26000 },
  { "Mage 27", "Mage 27ess", 27000 },
  { "Mage 28", "Mage 28ess", 28000 },
  { "Mage 29", "Mage 29ess", 29000 },
  { "Mage 30", "Mage 30ess", 30000 },
  { "Mage 31", "Mage 31ess", 31000 },
  { "Mage 32", "Mage 32ess", 32000 },
  { "Mage 33", "Mage 33ess", 33000 },
  { "Mage 34", "Mage 34ess", 34000 },
  { "Mage 35", "Mage 35ess", 35000 },
  { "Mage 36", "Mage 36ess", 36000 },
  { "Mage 37", "Mage 37ess", 37000 },
  { "Mage 38", "Mage 38ess", 38000 },
  { "Mage 39", "Mage 39ess", 39000 }
}, /* End Magic-User */
    /* Warrior */
{
  { "Fighter 0", "Fighter 0", 1 },
  { "Fighter 1", "Fighter 1", 2000 },
  { "Fighter 2", "Fighter 2", 4000 },
  { "Fighter 3", "Fighter 3", 6000 },
  { "Fighter 4", "Fighter 4", 8000 },
  { "Fighter 5", "Fighter 5", 10000 },
  { "Fighter 6", "Fighter 6", 12000 },
  { "Fighter 7", "Fighter 7", 14000 },
  { "Fighter 8", "Fighter 8", 16000 },
  { "Fighter 9", "Fighter 9", 18000 },
  { "Fighter 10", "Fighter 10", 20000 },
  { "Fighter 11", "Fighter 11", 22000 },
  { "Fighter 12", "Fighter 12", 24000 },
  { "Fighter 13", "Fighter 13", 26000 },
  { "Fighter 14", "Fighter 14", 28000 },
  { "Fighter 15", "Fighter 15", 30000 },
  { "Fighter 16", "Fighter 16", 32000 },
  { "Fighter 17", "Fighter 17", 34000 },
  { "Fighter 18", "Fighter 18", 36000 },
  { "Fighter 19", "Fighter 19", 38000 },
  { "Fighter 20", "Fighter 20", 40000 },
  { "Fighter 21", "Fighter 21", 42000 },
  { "Fighter 22", "Fighter 22", 44000 },
  { "Fighter 23", "Fighter 23", 46000 },
  { "Fighter 24", "Fighter 24", 48000 },
  { "Fighter 25", "Fighter 25", 50000 },
  { "Fighter 26", "Fighter 26", 52000 },
  { "Fighter 27", "Fighter 27", 54000 },
  { "Fighter 28", "Fighter 28", 56000 },
  { "Fighter 29", "Fighter 29", 58000 },
  { "Fighter 30", "Fighter 30", 60000 },
  { "Fighter 31", "Fighter 31", 62000 },
  { "Fighter 32", "Fighter 32", 64000 },
  { "Fighter 33", "Fighter 33", 66000 },
  { "Fighter 34", "Fighter 34", 68000 },
  { "Fighter 35", "Fighter 35", 70000 },
  { "Fighter 36", "Fighter 36", 72000 },
  { "Fighter 37", "Fighter 37", 74000 },
  { "Fighter 38", "Fighter 38", 76000 },
  { "Fighter 39", "Fighter 39", 78000 }
}, /* End Warrior */
    /* Unused UN */
{
  { "Unused 0", "Unused 0", 1 },
  { "Unused 1", "Unused 1", 1000 },
  { "Unused 2", "Unused 2", 2000 },
  { "Unused 3", "Unused 3", 3000 },
  { "Unused 4", "Unused 4", 4000 },
  { "Unused 5", "Unused 5", 5000 },
  { "Unused 6", "Unused 6", 6000 },
  { "Unused 7", "Unused 7", 7000 },
  { "Unused 8", "Unused 8", 8000 },
  { "Unused 9", "Unused 9", 9000 },
  { "Unused 10", "Unused 10", 10000 },
  { "Unused 11", "Unused 11", 11000 },
  { "Unused 12", "Unused 12", 12000 },
  { "Unused 13", "Unused 13", 13000 },
  { "Unused 14", "Unused 14", 14000 },
  { "Unused 15", "Unused 15", 15000 },
  { "Unused 16", "Unused 16", 16000 },
  { "Unused 17", "Unused 17", 17000 },
  { "Unused 18", "Unused 18", 18000 },
  { "Unused 19", "Unused 19", 19000 },
  { "Unused 20", "Unused 20", 20000 },
  { "Unused 21", "Unused 21", 21000 },
  { "Unused 22", "Unused 22", 22000 },
  { "Unused 23", "Unused 23", 23000 },
  { "Unused 24", "Unused 24", 24000 },
  { "Unused 25", "Unused 25", 25000 },
  { "Unused 26", "Unused 26", 26000 },
  { "Unused 27", "Unused 27", 27000 },
  { "Unused 28", "Unused 28", 28000 },
  { "Unused 29", "Unused 29", 29000 },
  { "Unused 30", "Unused 30", 30000 },
  { "Unused 31", "Unused 31", 31000 },
  { "Unused 32", "Unused 32", 32000 },
  { "Unused 33", "Unused 33", 33000 },
  { "Unused 34", "Unused 34", 34000 },
  { "Unused 35", "Unused 35", 35000 },
  { "Unused 36", "Unused 36", 36000 },
  { "Unused 37", "Unused 37", 37000 },
  { "Unused 38", "Unused 38", 38000 },
  { "Unused 39", "Unused 39", 39000 }
}
};

const char	*pc_class_types[] = {
   "UNDEFINED",
   "magic-user",
   "warrior",
   "DO_NOT_USE",
   "dual-class",
   "\n"
};
