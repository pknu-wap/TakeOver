using System;
using System.IO;

// Preserve the existing entry point; readability and UI interaction have their own suite.
public static class HelpVerification
{
    public static string Output => Path.GetFullPath(Environment.GetEnvironmentVariable("TAKEOVER_QA_OUTPUT") ?? "../QA_Help") + Path.DirectorySeparatorChar;
    public static void Run() => UiLayoutVerification.Run();
}
