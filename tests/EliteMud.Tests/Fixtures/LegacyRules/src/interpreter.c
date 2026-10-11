/* Cut-down interpreter.c for LegacyRulesImporterTests: the race menu */

  case CON_QRACE:
    switch (*arg) {
    case 'a': GET_RACE(d->character) = RACE_HUMAN; break;
    case 'b': GET_RACE(d->character) = RACE_DWARF; break;
    }
