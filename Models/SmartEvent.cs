namespace NI.Models
{
    public enum SmartEventPriority
    {
        System = 0,   // Highest - Windows notifications
        Spotify = 1,  // High - Now playing (overrides smart cards)
        Smart = 2,    // Medium - Birthdays, holidays
        Idle = 3      // Lowest - Rotating messages
    }

    public class SmartEvent
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Icon { get; set; } = "📌";
        public SmartEventPriority Priority { get; set; } = SmartEventPriority.Idle;

        public SmartEvent() { }

        public SmartEvent(string title, string message, string icon, SmartEventPriority priority)
        {
            Title = title;
            Message = message;
            Icon = icon;
            Priority = priority;
        }
    }
}
