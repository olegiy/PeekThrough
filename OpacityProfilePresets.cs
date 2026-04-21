using System;
using System.Collections.Generic;
using System.Linq;
using GhostThrough.Models;

namespace GhostThrough
{
    /// <summary>
    /// Shared default opacity presets used for first launch and legacy settings migration.
    /// </summary>
    internal static class OpacityProfilePresets
    {
        private static readonly byte[] DefaultOpacities = new byte[]
        {
            26, 51, 76, 102, 128, 153, 178, 204, 230
        };

        public static string DefaultActiveProfileId
        {
            get { return BuildProfileId(DefaultOpacities[0]); }
        }

        public static List<Profile> CreateDefaultProfiles()
        {
            return DefaultOpacities
                .Select(opacity => new Profile(BuildProfileId(opacity), BuildProfileName(opacity), opacity))
                .ToList();
        }

        public static List<ProfileData> CreateDefaultProfileDataList()
        {
            return DefaultOpacities
                .Select(opacity => new ProfileData
                {
                    Id = BuildProfileId(opacity),
                    Name = BuildProfileName(opacity),
                    Opacity = opacity
                })
                .ToList();
        }

        public static string GetClosestProfileId(byte opacity)
        {
            byte closestOpacity = DefaultOpacities
                .OrderBy(value => Math.Abs(value - opacity))
                .First();

            return BuildProfileId(closestOpacity);
        }

        private static string BuildProfileId(byte opacity)
        {
            return string.Format("p{0}", GetOpacityPercent(opacity));
        }

        private static string BuildProfileName(byte opacity)
        {
            return string.Format("{0}%", GetOpacityPercent(opacity));
        }

        private static int GetOpacityPercent(byte opacity)
        {
            return (int)Math.Round(opacity * 100.0 / 255.0, MidpointRounding.AwayFromZero);
        }
    }
}
