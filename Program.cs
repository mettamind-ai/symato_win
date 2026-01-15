namespace SymatoIME;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main(string[] args)
    {
        // Run tests if --test flag is passed
        if (args.Length > 0 && args[0] == "--test")
        {
            EngineTests.RunAll();
            return;
        }
        
        // Ensure single instance
        const string mutexName = "SymatoIME_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        
        if (!createdNew)
        {
            MessageBox.Show("SymatoIME is already running!", "SymatoIME", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new SymatoContext());
        
        _mutex?.ReleaseMutex();
    }
}
