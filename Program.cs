namespace Coffee;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var app = new CoffeeApp();
        Application.Run();
    }
}
