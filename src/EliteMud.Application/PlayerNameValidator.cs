namespace EliteMud.Application;

public sealed class PlayerNameValidator
{
    public bool IsValid(string name)
    {
        if (name.Length is < 3 or > 16)
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsLetter(character))
            {
                return false;
            }
        }

        return true;
    }
}
