/* Cut-down structs.h for LegacyRulesImporterTests: only the defines the rules import reads */

#define CLASS_MAGIC_USER   1  /* Magic User type */
#define CLASS_WARRIOR      2  /* Warrior type    */
#define CLASS_UNUSED       3
#define CLASS_DUAL         4

#define MU_CLASS          (1 << 0)
#define WA_CLASS          (1 << 1)
#define UN_CLASS          (1 << 2)

#define RACE_GOD          0
#define RACE_HUMAN        1
#define RACE_DWARF        2
#define RACE_ANGEL        3

#define LEVEL_DEITY     110
