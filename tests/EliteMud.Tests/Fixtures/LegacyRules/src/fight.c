/* Trimmed from the legacy fight.c: only what the rules importer reads. */

int get_thaco(struct char_data *ch)
{
    int i, class[3], level[3], thaco[3];

    if (IS_NPC(ch))
	return (20 - GET_HITROLL(ch));

    for (i = 0;i < 3;i++)
	switch (class[i])
	{
	case CLASS_MAGIC_USER:
	    thaco[i] = (20 - level[i] / 4);
	    break;
	case CLASS_WARRIOR:
	    thaco[i] = (20 - level[i] / 3);
	    break;
	default:
	    thaco[i] = 20;
	}

    return MIN(thaco[0], MIN(thaco[1], thaco[2]));
}
