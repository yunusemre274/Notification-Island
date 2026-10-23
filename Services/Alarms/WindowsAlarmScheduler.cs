using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using NI.Agent.Alarms;

namespace NI.Services.Alarms;

// Only our current executable + validated GUID/ticks is scheduled. No shell/script, no elevation.
public sealed class WindowsAlarmScheduler : IAlarmScheduler
{
    private const string Prefix = "NotificationIsland.Alarm.";
    private readonly string _executable = Environment.ProcessPath ?? throw new InvalidOperationException("Uygulama executable yolu yok.");
    private string? _userSid;
    private string _sid { get { using var identity = WindowsIdentity.GetCurrent(); return _userSid ??= identity.User?.Value ?? throw new InvalidOperationException("Kullanıcı SID bulunamadı."); } }
    private string Name(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Geçersiz alarm kimliği.");
        return Prefix + _sid + "." + id;
    }
    public void Schedule(AlarmRecord alarm)
    {
        dynamic? service = null, folder = null, task = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            service!.Connect(); folder = service.GetFolder("\\");
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            XElement E(string name, object? value) => new(ns + name, value);
            var args = $"--alarm-fire {alarm.Id} {alarm.NextDue.UtcTicks.ToString(CultureInfo.InvariantCulture)}";
            var xml = new XDocument(new XElement(ns + "Task", new XAttribute("version", "1.2"),
                E("RegistrationInfo", E("Description", "Notification Island doğrulanmış alarm")),
                E("Triggers", E("TimeTrigger", new object[] { E("StartBoundary", alarm.NextDue.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)), E("EndBoundary", alarm.NextDue.AddHours(24).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)), E("Enabled", "true") })),
                E("Principals", new XElement(ns + "Principal", new XAttribute("id", "Author"), E("UserId", _sid), E("LogonType", "InteractiveToken"), E("RunLevel", "LeastPrivilege"))),
                E("Settings", new object[] { E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", "false"), E("StopIfGoingOnBatteries", "false"), E("StartWhenAvailable", "true"), E("RunOnlyIfNetworkAvailable", "false"), E("Enabled", "true"), E("WakeToRun", "false"), E("ExecutionTimeLimit", "PT10M") }),
                new XElement(ns + "Actions", new XAttribute("Context", "Author"), E("Exec", new object[] { E("Command", _executable), E("Arguments", args) }))));
            task = folder.RegisterTask(Name(alarm.Id), xml.ToString(SaveOptions.DisableFormatting), 6, null, null, 3, null);
        }
        finally { Release(task); Release(folder); Release(service); }
    }
    public bool IsScheduled(AlarmRecord alarm)
    {
        dynamic? service = null, folder = null, task = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!); service!.Connect(); folder = service.GetFolder("\\");
            try { task = folder.GetTask(Name(alarm.Id)); } catch (COMException ex) when ((uint)ex.HResult is 0x80070002 or 0x8004130F) { return false; }
            var xml = XDocument.Parse((string)task.Xml);
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            return xml.Descendants(ns + "Command").Single().Value.Equals(_executable, StringComparison.OrdinalIgnoreCase)
                && xml.Descendants(ns + "Arguments").Single().Value == $"--alarm-fire {alarm.Id} {alarm.NextDue.UtcTicks.ToString(CultureInfo.InvariantCulture)}"
                && xml.Descendants(ns + "UserId").Single().Value == _sid
                && xml.Descendants(ns + "RunLevel").Single().Value == "LeastPrivilege"
                && xml.Descendants(ns + "LogonType").Single().Value == "InteractiveToken"
                && Math.Abs((DateTimeOffset.Parse(xml.Descendants(ns + "StartBoundary").Single().Value, CultureInfo.InvariantCulture) - alarm.NextDue).TotalSeconds) < 1
                && xml.Descendants(ns + "Actions").Single().Elements().Count() == 1
                && (bool)task.Enabled;
        }
        finally { Release(task); Release(folder); Release(service); }
    }
    public void Cancel(string id)
    {
        dynamic? service = null, folder = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!); service!.Connect(); folder = service.GetFolder("\\");
            try { folder.DeleteTask(Name(id), 0); } catch (COMException ex) when ((uint)ex.HResult is 0x80070002 or 0x8004130F) { }
        }
        finally { Release(folder); Release(service); }
    }
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
