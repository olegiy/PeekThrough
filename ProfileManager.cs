using System;
using System.Collections.Generic;
using System.Linq;
using PeekThrough.Models;

namespace PeekThrough
{
    /// <summary>
    /// Manages opacity profiles (Min/Med/Max) with cycling support
    /// </summary>
    internal class ProfileManager
    {
        private readonly List<Profile> _profiles;
        private int _activeIndex;

        public IReadOnlyList<Profile> Profiles
        {
            get { return _profiles.AsReadOnly(); }
        }

        public Profile ActiveProfile
        {
            get { return _profiles[_activeIndex]; }
        }

        public byte CurrentOpacity
        {
            get { return ActiveProfile.Opacity; }
        }

        public event Action<Profile> OnProfileChanged;

        public ProfileManager(IEnumerable<Profile> profiles = null)
        {
            // Default profiles if none provided
            if (profiles == null)
            {
                _profiles = new List<Profile>
                {
                    new Profile("min", "Minimum", 38),
                    new Profile("med", "Medium", 128),
                    new Profile("max", "Maximum", 204)
                };
            }
            else
            {
                _profiles = profiles.ToList();
            }
            _activeIndex = 0;
        }

        public ProfileManager(string activeId, IEnumerable<Profile> profiles = null)
            : this(profiles)
        {
            SetActiveProfile(activeId);
        }

        public void SwitchToNextProfile()
        {
            _activeIndex = (_activeIndex + 1) % _profiles.Count;
            NotifyProfileChanged();
        }

        public void SwitchToPreviousProfile()
        {
            _activeIndex = (_activeIndex - 1 + _profiles.Count) % _profiles.Count;
            NotifyProfileChanged();
        }

        public void SetActiveProfile(string profileId)
        {
            int index = _profiles.FindIndex(p => p.Id == profileId);
            if (index >= 0)
            {
                _activeIndex = index;
                NotifyProfileChanged();
            }
        }

        public void SetActiveProfileByIndex(int index)
        {
            if (index >= 0 && index < _profiles.Count)
            {
                _activeIndex = index;
                NotifyProfileChanged();
            }
        }

        private void NotifyProfileChanged()
        {
            DebugLogger.Log(string.Format("ProfileManager: Switched to {0}", ActiveProfile));
            if (OnProfileChanged != null)
                OnProfileChanged(ActiveProfile);
        }
    }
}
