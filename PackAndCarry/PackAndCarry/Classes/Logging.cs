﻿using IVSDKDotNet;

namespace PackAndCarry.Classes
{
    internal class Logging
    {

        public static void Log(string str, params object[] args)
        {
            IVGame.Console.Print(string.Format("[PackAndCarry] {0}", string.Format(str, args)));
        }
        public static void LogWarning(string str, params object[] args)
        {
            IVGame.Console.PrintWarning(string.Format("[PackAndCarry] {0}", string.Format(str, args)));
        }
        public static void LogError(string str, params object[] args)
        {
            IVGame.Console.PrintError(string.Format("[PackAndCarry] {0}", string.Format(str, args)));
        }

        public static void LogDebug(string str, params object[] args)
        {
#if DEBUG
            IVGame.Console.Print(string.Format("[PackAndCarry] [DEBUG] {0}", string.Format(str, args)));
#endif
        }

    }
}
