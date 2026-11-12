public static class ImageCounters { public static int Initializers, Calls; }
public static class ImageTarget
{
    static ImageTarget() { ImageCounters.Initializers++; }
    public static int Run()
    {
        ImageCounters.Calls++;
#if ALTERNATE
        return 9;
#else
        return 7;
#endif
    }
}
