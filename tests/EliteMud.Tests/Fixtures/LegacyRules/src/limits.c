/* Cut-down limits.c for LegacyRulesImporterTests */

void improve_abilities(struct char_data *ch, int class)
{
    int str = 25, con = 25, dex = 25, intel = 25, wis = 25, cha = 25;
    bool stradd = FALSE;

    switch (class) {
    case CLASS_MAGIC_USER:
	intel = 0;
	wis = 0;
	break;
    case CLASS_WARRIOR:
        stradd = TRUE;
	str = 0;
	con = 0;
	break;
    }
}

void	advance_level(struct char_data *ch, int log)
{
   int	add_hp, i, add_mana = 0, div;

   add_hp = number(1, 5);

   switch (LOWEST_CLASS(ch)) {

   case CLASS_MAGIC_USER :
       add_hp += GET_CON(ch)/5;
       add_mana = 10;
       break;

   case CLASS_WARRIOR :
       add_hp += GET_CON(ch)/2;
       add_mana = 5;
       break;
   }

   ch->points.max_hit += MAX(1, add_hp / div);
   if ((GET_LEVEL(ch) != 1)) {
      ch->points.max_mana += MAX(1, add_mana/div);
   } /* if */
   ch->points.max_move += MAX(1, 3 / div );
}
