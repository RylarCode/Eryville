// Where the game finds its backend. Starts as the local one; the online one will be put here at startup.
public static class Backend
{
    private static IGameBackend current;

    public static IGameBackend Current
    {
        get
        {
            if (current == null) current = new LocalBackend();
            return current;
        }
        set { current = value; }
    }
}
