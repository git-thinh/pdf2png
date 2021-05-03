using System.IO;

public static class StaticDocument
{
    public static long BuildId(DOC_TYPE type, int pageTotal, long fileSize)
    {
        string s = string.Empty;
        switch (pageTotal.ToString().Length)
        {
            default: s = "00000"; break;
            case 1: s = "0000" + s; break;
            case 2: s = "000" + s; break;
            case 3: s = "00" + s; break;
            case 4: s = "0" + s; break;
            case 5: s = "" + s; break;
        }
        string key = string.Format("{0}{1}{2}", (int)type, s, fileSize);
        return long.Parse(key);
    }
}

