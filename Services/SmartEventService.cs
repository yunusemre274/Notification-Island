using System;
using System.Collections.Generic;
using NI.Models;

namespace NI.Services
{
    /// <summary>
    /// Smart Event Service - Generates predictive cards based on dates.
    /// OPTIMIZED: No internal timer - called on-demand by main timer.
    /// Caches current event to avoid recalculation.
    /// </summary>
    public class SmartEventService
    {
        private readonly AppSettings _settings;
        private DateTime _lastCheckDate = DateTime.MinValue;
        private SmartEvent? _cachedEvent;
        private int _idleMessageIndex = 0;
        private DateTime _sessionStartTime;
        private readonly Random _random = new();

        public event EventHandler<SmartEvent>? SmartEventGenerated;

        // Turkish National Days (month, day, name)
        private static readonly List<(int Month, int Day, string Name)> NationalDays = new()
        {
            (11, 10, "Atatürk'ü Anma Günü"),
            (10, 29, "Cumhuriyet Bayramı"),
            (8, 30, "Zafer Bayramı"),
            (5, 19, "Atatürk'ü Anma, Gençlik ve Spor Bayramı"),
            (4, 23, "Ulusal Egemenlik ve Çocuk Bayramı"),
            (7, 15, "Demokrasi ve Milli Birlik Günü")
        };

        // Global Days (month, day, name)
        private static readonly List<(int Month, int Day, string Name)> GlobalDays = new()
        {
            (1, 1, "Yeni Yıl"),
            (2, 14, "Sevgililer Günü"),
            (3, 8, "Dünya Kadınlar Günü"),
            (11, 24, "Öğretmenler Günü"),
        };

        // Idle messages for rotation
        private static readonly List<string> IdleMessages = new()
        {
            "💧 Su içmeyi unutmayın.",
            "🧘 Kısa bir mola verin, gözlerinizi dinlendirin.",
            "🌟 Bugün harika işler başarabilirsiniz!",
            "☕ Bir kahve molası zamanı mı?",
            "🎯 Odaklanın, başarı yakın!",
            "🌈 Pozitif düşünün, güzel şeyler olacak.",
            "📚 Yeni bir şey öğrenmek için harika bir gün!",
            "💪 Harekete geçin, biraz esneme yapın.",
            "🎵 Biraz müzik motivasyonu artırır!",
            "🌸 Kendinize iyi davranın."
        };

        public SmartEventService(AppSettings settings)
        {
            _settings = settings;
            _sessionStartTime = DateTime.Now;
        }

        /// <summary>
        /// Called by main timer. Only recalculates if date changed.
        /// </summary>
        public void CheckSmartEvents()
        {
            var today = DateTime.Today;
            
            // Only recalculate if date changed
            if (_lastCheckDate == today && _cachedEvent != null)
                return;

            _lastCheckDate = today;
            var tomorrow = today.AddDays(1);

            // Check birthday first
            var evt = CheckBirthday(today, tomorrow);
            if (evt != null)
            {
                _cachedEvent = evt;
                SmartEventGenerated?.Invoke(this, evt);
                return;
            }

            // Check national days
            if (_settings.ShowNationalEvents)
            {
                evt = CheckNationalDays(today, tomorrow);
                if (evt != null)
                {
                    _cachedEvent = evt;
                    SmartEventGenerated?.Invoke(this, evt);
                    return;
                }
            }

            // Check global days
            if (_settings.ShowGlobalEvents)
            {
                evt = CheckGlobalDays(today, tomorrow);
                if (evt != null)
                {
                    _cachedEvent = evt;
                    SmartEventGenerated?.Invoke(this, evt);
                    return;
                }
            }

            _cachedEvent = null;
        }

        /// <summary>
        /// Called by main timer every ~45 seconds for idle messages.
        /// </summary>
        public void RotateIdleMessage()
        {
            if (!_settings.IdleMessagesEnabled) return;

            var sessionDuration = DateTime.Now - _sessionStartTime;
            string message;
            
            // Every 5th rotation, show session time
            if (_idleMessageIndex % 5 == 4 && sessionDuration.TotalMinutes >= 30)
            {
                var hours = (int)sessionDuration.TotalHours;
                var minutes = sessionDuration.Minutes;
                message = hours > 0 
                    ? $"⌛ Bilgisayarı {hours} saat {minutes} dakikadır kullanıyorsunuz."
                    : $"⌛ Bilgisayarı {minutes} dakikadır kullanıyorsunuz.";
            }
            else
            {
                message = IdleMessages[_idleMessageIndex % IdleMessages.Count];
            }

            _idleMessageIndex++;
            SmartEventGenerated?.Invoke(this, new SmartEvent("Hatırlatma", message, "💡", SmartEventPriority.Idle));
        }

        private SmartEvent? CheckBirthday(DateTime today, DateTime tomorrow)
        {
            var birthday = _settings.GetBirthdayDate();
            if (birthday == null) return null;

            var thisYearBirthday = new DateTime(today.Year, birthday.Value.Month, birthday.Value.Day);
            if (thisYearBirthday < today)
                thisYearBirthday = thisYearBirthday.AddYears(1);

            var daysUntil = (thisYearBirthday - today).Days;

            return daysUntil switch
            {
                0 => new SmartEvent("Doğum Günü", "🎂 İyi ki doğdunuz! Mutlu yıllar!", "🎂", SmartEventPriority.Smart),
                1 => new SmartEvent("Doğum Günü", "🎉 Yarın doğum gününüz!", "🎉", SmartEventPriority.Smart),
                <= 7 => new SmartEvent("Doğum Günü", $"🎈 Doğum gününüze {daysUntil} gün kaldı!", "🎈", SmartEventPriority.Smart),
                <= 30 => new SmartEvent("Doğum Günü", "🎁 Doğum gününüz yaklaşıyor!", "🎁", SmartEventPriority.Smart),
                _ => null
            };
        }

        private SmartEvent? CheckNationalDays(DateTime today, DateTime tomorrow)
        {
            foreach (var (month, day, name) in NationalDays)
            {
                if (today.Month == month && today.Day == day)
                    return new SmartEvent(name, $"Bugün {name} 🇹🇷", "🇹🇷", SmartEventPriority.Smart);
                if (tomorrow.Month == month && tomorrow.Day == day)
                    return new SmartEvent(name, $"Yarın {name} 🇹🇷", "🇹🇷", SmartEventPriority.Smart);
            }
            return null;
        }

        private SmartEvent? CheckGlobalDays(DateTime today, DateTime tomorrow)
        {
            foreach (var (month, day, name) in GlobalDays)
            {
                if (today.Month == month && today.Day == day)
                    return new SmartEvent(name, $"Bugün {name}! 🌍", "🌍", SmartEventPriority.Smart);
                if (tomorrow.Month == month && tomorrow.Day == day)
                    return new SmartEvent(name, $"Yarın {name}! 🌍", "🌍", SmartEventPriority.Smart);
            }

            // Check Mother's Day (2nd Sunday of May)
            var mothersDay = GetNthSundayOfMonth(today.Year, 5, 2);
            if (today == mothersDay)
                return new SmartEvent("Anneler Günü", "Bugün Anneler Günü! 💐", "💐", SmartEventPriority.Smart);
            if (tomorrow == mothersDay)
                return new SmartEvent("Anneler Günü", "Yarın Anneler Günü! 💐", "💐", SmartEventPriority.Smart);

            // Check Father's Day (3rd Sunday of June)
            var fathersDay = GetNthSundayOfMonth(today.Year, 6, 3);
            if (today == fathersDay)
                return new SmartEvent("Babalar Günü", "Bugün Babalar Günü! 👔", "👔", SmartEventPriority.Smart);
            if (tomorrow == fathersDay)
                return new SmartEvent("Babalar Günü", "Yarın Babalar Günü! 👔", "👔", SmartEventPriority.Smart);

            return null;
        }

        private static DateTime GetNthSundayOfMonth(int year, int month, int n)
        {
            var firstDay = new DateTime(year, month, 1);
            var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)firstDay.DayOfWeek + 7) % 7;
            return firstDay.AddDays(daysUntilSunday + (n - 1) * 7);
        }

        public SmartEvent GetCurrentSmartEvent()
        {
            if (_cachedEvent != null) return _cachedEvent;

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var evt = CheckBirthday(today, tomorrow);
            if (evt != null) return evt;

            if (_settings.ShowNationalEvents)
            {
                evt = CheckNationalDays(today, tomorrow);
                if (evt != null) return evt;
            }

            if (_settings.ShowGlobalEvents)
            {
                evt = CheckGlobalDays(today, tomorrow);
                if (evt != null) return evt;
            }

            var msg = IdleMessages[_random.Next(IdleMessages.Count)];
            return new SmartEvent("Hazır", msg, "✨", SmartEventPriority.Idle);
        }
    }
}
