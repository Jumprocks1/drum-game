namespace FFMediaToolkit.Helpers
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using FFMediaToolkit.Common;
    using FFmpeg.AutoGen;

    /// <summary>
    /// Contains extension methods.
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Checks if specified integer is error code and throws an <see cref="FFmpegException"/>.
        /// </summary>
        /// <param name="errorCode">The exit code returned by a method.</param>
        /// <param name="exceptionMessage">The exception message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ThrowIfError(this int errorCode, string exceptionMessage)
        {
            if (errorCode < 0)
            {
                throw new FFmpegException(exceptionMessage, errorCode);
            }
        }

        /// <summary>
        /// Gets the <see cref="DescriptionAttribute"/> value of the specified enumeration value.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The description attribute string of this enum value.</returns>
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            return Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute
                ? attribute.Description : value.ToString();
        }

        /// <summary>
        /// Checks if this object is equal to at least one of specified objects.
        /// </summary>
        /// <typeparam name="T">Type of the objects.</typeparam>
        /// <param name="value">This object.</param>
        /// <param name="valueToCompare">Objects to check.</param>
        /// <returns><see langword="true"/> is the object is equal to at least one of specified objects.</returns>
        public static bool IsMatch<T>(this T value, params T[] valueToCompare)
            where T : struct, Enum
        {
            foreach (T x in valueToCompare)
            {
                if (value.Equals(x))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
