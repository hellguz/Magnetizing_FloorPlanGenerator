using System;

namespace Magnetizing_FPG.Core.Domain
{
    /// <summary>
    /// HashCode helper for .NET Framework 4.8 compatibility.
    /// This provides the HashCode.Combine functionality that's available in .NET Core but not .NET Framework.
    /// </summary>
    internal static class HashCode
    {
        /// <summary>
        /// Combines hash codes of two objects.
        /// </summary>
        public static int Combine<T1, T2>(T1 value1, T2 value2)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (value1?.GetHashCode() ?? 0);
                hash = hash * 23 + (value2?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Combines hash codes of three objects.
        /// </summary>
        public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (value1?.GetHashCode() ?? 0);
                hash = hash * 23 + (value2?.GetHashCode() ?? 0);
                hash = hash * 23 + (value3?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Combines hash codes of four objects.
        /// </summary>
        public static int Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (value1?.GetHashCode() ?? 0);
                hash = hash * 23 + (value2?.GetHashCode() ?? 0);
                hash = hash * 23 + (value3?.GetHashCode() ?? 0);
                hash = hash * 23 + (value4?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Combines hash codes of five objects.
        /// </summary>
        public static int Combine<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (value1?.GetHashCode() ?? 0);
                hash = hash * 23 + (value2?.GetHashCode() ?? 0);
                hash = hash * 23 + (value3?.GetHashCode() ?? 0);
                hash = hash * 23 + (value4?.GetHashCode() ?? 0);
                hash = hash * 23 + (value5?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Combines hash codes of six objects.
        /// </summary>
        public static int Combine<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (value1?.GetHashCode() ?? 0);
                hash = hash * 23 + (value2?.GetHashCode() ?? 0);
                hash = hash * 23 + (value3?.GetHashCode() ?? 0);
                hash = hash * 23 + (value4?.GetHashCode() ?? 0);
                hash = hash * 23 + (value5?.GetHashCode() ?? 0);
                hash = hash * 23 + (value6?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Combines hash codes of seven objects.
        /// </summary>
        public static int Combine<T1, T2, T3, T4, T5, T6, T7>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (value1?.GetHashCode() ?? 0);
                hash = hash * 23 + (value2?.GetHashCode() ?? 0);
                hash = hash * 23 + (value3?.GetHashCode() ?? 0);
                hash = hash * 23 + (value4?.GetHashCode() ?? 0);
                hash = hash * 23 + (value5?.GetHashCode() ?? 0);
                hash = hash * 23 + (value6?.GetHashCode() ?? 0);
                hash = hash * 23 + (value7?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Combines hash codes of eight objects.
        /// </summary>
        public static int Combine<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (value1?.GetHashCode() ?? 0);
                hash = hash * 23 + (value2?.GetHashCode() ?? 0);
                hash = hash * 23 + (value3?.GetHashCode() ?? 0);
                hash = hash * 23 + (value4?.GetHashCode() ?? 0);
                hash = hash * 23 + (value5?.GetHashCode() ?? 0);
                hash = hash * 23 + (value6?.GetHashCode() ?? 0);
                hash = hash * 23 + (value7?.GetHashCode() ?? 0);
                hash = hash * 23 + (value8?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}