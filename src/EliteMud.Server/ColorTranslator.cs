namespace EliteMud.Server;

/// <summary>
/// Translates EliteMUD color codes (#X format) to ANSI escape sequences for telnet display.
/// Based on legacy scrcol.c/scrcol.h color handling.
/// </summary>
public static class ColorTranslator
{
    // ANSI escape codes from legacy scrcol.h
    private const string VT_COLNRM = "\x1B[0;37m";  // Normal white
    private const string VT_COLRED = "\x1B[0;31m";  // Dark red
    private const string VT_COLGRN = "\x1B[0;32m";  // Dark green
    private const string VT_COLYEL = "\x1B[0;33m";  // Dark yellow
    private const string VT_COLBLU = "\x1B[0;34m";  // Dark blue
    private const string VT_COLMAG = "\x1B[0;35m";  // Dark magenta
    private const string VT_COLCYN = "\x1B[0;36m";  // Dark cyan
    private const string VT_COLWHT = "\x1B[0;37m";  // Dark white
    
    private const string VT_COLLRED = "\x1B[1;31m"; // Light red (bright)
    private const string VT_COLLGRN = "\x1B[1;32m"; // Light green (bright)
    private const string VT_COLLYEL = "\x1B[1;33m"; // Light yellow (bright)
    private const string VT_COLLBLU = "\x1B[1;34m"; // Light blue (bright)
    private const string VT_COLLMAG = "\x1B[1;35m"; // Light magenta (bright)
    private const string VT_COLLCYN = "\x1B[1;36m"; // Light cyan (bright)
    private const string VT_COLLWHT = "\x1B[1;37m"; // Light white (bright)
    private const string VT_COLLBLK = "\x1B[1;30m"; // Light black (bright gray)
    
    private const string VT_OFF = "\x1B[0m";        // Attributes off
    
    /// <summary>
    /// Translates color codes from #X format to ANSI escape sequences.
    /// Matches legacy behavior from scrcol.c line 36-150.
    /// </summary>
    public static string TranslateColors(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
            
        // Quick check - if no # character, return as-is
        if (!text.Contains('#'))
            return text;
        
        var result = new System.Text.StringBuilder(text.Length + 100);
        
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '#' && i + 1 < text.Length)
            {
                char code = text[i + 1];
                string? ansiCode = code switch
                {
                    // Special codes
                    'N' => VT_COLNRM,     // Normal/reset
                    '0' => VT_OFF,        // All attributes off
                    '#' => "#",           // Literal # character
                    
                    // Dark colors (uppercase)
                    'R' => VT_COLRED,     // Dark red
                    'G' => VT_COLGRN,     // Dark green
                    'Y' => VT_COLYEL,     // Dark yellow
                    'B' => VT_COLBLU,     // Dark blue
                    'M' => VT_COLMAG,     // Dark magenta
                    'C' => VT_COLCYN,     // Dark cyan
                    'W' => VT_COLWHT,     // Dark white
                    
                    // Light/bright colors (lowercase)
                    'r' => VT_COLLRED,    // Light red
                    'g' => VT_COLLGRN,    // Light green
                    'y' => VT_COLLYEL,    // Light yellow
                    'b' => VT_COLLBLU,    // Light blue
                    'm' => VT_COLLMAG,    // Light magenta
                    'c' => VT_COLLCYN,    // Light cyan
                    'w' => VT_COLLWHT,    // Light white
                    'e' => VT_COLLBLK,    // Light black (gray)
                    
                    // Unknown code - keep as-is
                    _ => null
                };
                
                if (ansiCode != null)
                {
                    result.Append(ansiCode);
                    i++; // Skip the code character
                }
                else
                {
                    // Unknown code, keep the # character
                    result.Append('#');
                }
            }
            else
            {
                result.Append(text[i]);
            }
        }
        
        return result.ToString();
    }
}
