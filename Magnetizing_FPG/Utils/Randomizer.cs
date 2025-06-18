using System;

namespace Magnetizing_FPG.Utils
{
    /// <summary>
    /// Provides a single, shared instance of the Random class to avoid issues
    /// with multiple instances created in quick succession having the same seed.
    /// </summary>
    public static class Randomizer
    {
        private static readonly Random _instance = new Random();

        public static Random Instance => _instance;
    }
}

