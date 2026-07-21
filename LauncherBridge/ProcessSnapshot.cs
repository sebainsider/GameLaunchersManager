using System.Diagnostics;

namespace LauncherBridge;

public record ProcessInfo(int Id, string ProcessName);

public class ProcessSnapshot
{
    private readonly Dictionary<int, string> _processes;

    public ProcessSnapshot(IEnumerable<ProcessInfo> processes)
    {
        _processes = processes.ToDictionary(p => p.Id, p => p.ProcessName);
    }

    public IReadOnlyDictionary<int, string> Processes => _processes;

    public static ProcessSnapshot Capture()
    {
        var processInfos = new List<ProcessInfo>();
        var allProcesses = Process.GetProcesses();

        foreach (var proc in allProcesses)
        {
            try
            {
                processInfos.Add(new ProcessInfo(proc.Id, proc.ProcessName));
            }
            catch
            {
                // Access denied or exited process
            }
            finally
            {
                proc.Dispose();
            }
        }

        return new ProcessSnapshot(processInfos);
    }

    public List<ProcessInfo> GetNewProcesses(ProcessSnapshot latestSnapshot)
    {
        var newProcesses = new List<ProcessInfo>();

        foreach (var (id, name) in latestSnapshot.Processes)
        {
            if (!_processes.ContainsKey(id))
            {
                newProcesses.Add(new ProcessInfo(id, name));
            }
        }

        return newProcesses;
    }
}
